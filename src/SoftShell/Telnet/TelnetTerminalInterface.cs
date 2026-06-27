using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Telnet
{
    /// <summary>
    /// A terminal interface for communicating with Telnet clients.
    /// </summary>
    internal sealed class TelnetTerminalInterface : RemoteAnsiTerminalInterface
    {
        private enum CommandState
        {
            None = 0,
            Begin, // IAC received
            Will,
            Wont,
            Do,
            Dont,
            SubNegociationBegin,
            SubNegociationNaws
        }

        // Key escape sequences: https://www.novell.com/documentation/extend5/Docs/help/Composer/books/TelnetAppendixB.html

        private const byte SE   = 240; // Sub-negociation end
        private const byte SB   = 250; // Sub-negociation begin
        private const byte WILL = 251;
        private const byte WONT = 252;
        private const byte DO   = 253;
        private const byte DONT = 254;
        private const byte IAC  = 255; // Interpret as command

        private const byte OPTION_ECHO = 1;
        private const byte OPTION_SUPPRESS_GO_AHEAD = 3;
        private const byte OPTION_NAWS = 31; // Negotiate About Window Size

        private Socket _socket;
        private NetworkStream _stream;
        private Task _writeTask;
        private Task _readTask;
        private CommandState _commandState = CommandState.None;
        private bool _echoToTerminal = false;

        private List<byte> _windowSizeBytesReceived = new List<byte>();

        /// <inheritdoc/>
        public override string TerminalType => "Telnet";

        /// <inheritdoc/>
        public override string TerminalInstanceInfo => _socket.RemoteEndPoint.ToString();

        /// <inheritdoc/>
        public override int? WindowWidth
        {
            get
            {
                if (_windowSizeBytesReceived.Count < 2)
                    return null;

                return (_windowSizeBytesReceived[0] << 8) + _windowSizeBytesReceived[1];
            }
        }

        /// <inheritdoc/>
        public override int? WindowHeight
        {
            get
            {
                if (_windowSizeBytesReceived.Count < 4)
                    return null;

                return (_windowSizeBytesReceived[2] << 8) + _windowSizeBytesReceived[3];
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="socket">Socket for the Telnet connection.</param>
        internal TelnetTerminalInterface(Socket socket)
        {
            _socket = socket;
            _stream = new NetworkStream(socket);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _workerTaskCancellation.Cancel();

            // Wait for the worker tasks to complete
            if (_readTask != null)
            {
                while (!_readTask.IsCompleted)
                {
                    Thread.Sleep(100);
                }
            }
            if (_writeTask != null)
            {
                while (!_writeTask.IsCompleted)
                {
                    Thread.Sleep(100);
                }
            }

            if (_stream != null)
                _stream.Dispose();

            if (_socket != null)
                _socket.Dispose();

            base.Dispose();
        }

        /// <summary>
        /// Starts Telnet communication.
        /// </summary>
        public void Start()
        {
            _readTask = Task.Run(() =>
            {
                var byteToRead  = new byte[1];

                while (!_workerTaskCancellation.IsCancellationRequested)
                {
                    try
                    {
                        if (_stream.ReadAsync(byteToRead, 0, 1, _workerTaskCancellation.Token).Result == 1)
                        {
                            if (!HandleByteAsCommand(byteToRead[0]))
                            {
                                var parsedResult = _ansiParser.HandleByte(byteToRead[0]);

                                foreach (var item in parsedResult)
                                {
                                    // Ctrl-C pressed?
                                    if (item.action == KeyAction.Character && item.character == (char)3)
                                        RequestTaskCancel();
                                    else
                                        _receivedItems.Enqueue(item);
                                }
                            }
                        }
                    }
                    catch { }
                }
            });

            _writeTask = Task.Run(() =>
            {
                var byteToWrite = new byte[1];

                SetSuppressGoAhead();
                SetTerminalSelfEcho(false);
                SetWindowsSizeNegociation();

                while (!_workerTaskCancellation.IsCancellationRequested)
                {
                    try
                    {
                        while (_bytesToSend.TryDequeue(out byteToWrite[0]))
                        {
                            SendBytes(byteToWrite, skipQueue: true);
                        }

                        Thread.Sleep(50);
                    }
                    catch { }
                }
            });
        }

        private bool HandleByteAsCommand(byte value)
        {
            switch (_commandState)
            {
                case CommandState.None:
                    if (value == IAC)
                    {
                        _commandState = CommandState.Begin;
                        return true;
                    }
                    break;

                case CommandState.Begin:
                    switch (value)
                    {
                        case WILL:
                            _commandState = CommandState.Will;
                            break;

                        case WONT:
                            _commandState = CommandState.Wont;
                            break;

                        case DO:
                            _commandState = CommandState.Do;
                            break;

                        case DONT:
                            _commandState = CommandState.Dont;
                            break;

                        case SB:
                            _commandState = CommandState.SubNegociationBegin;
                            break;

                        case SE:
                            _commandState = CommandState.None;
                            break;

                        default:
                            // Unhandled command
                            _commandState = CommandState.None;
                            break;
                    }
                    return true;

                case CommandState.Will:
                case CommandState.Wont:
                case CommandState.Do:
                case CommandState.Dont:
                    _commandState = CommandState.None;
                    return true;

                case CommandState.SubNegociationBegin:
                    if (value == OPTION_NAWS)
                    {
                        _windowSizeBytesReceived.Clear();
                        _commandState = CommandState.SubNegociationNaws;
                    }
                    else
                    {
                        _commandState = CommandState.None;
                    }
                    return true;

                case CommandState.SubNegociationNaws:
                    if (_windowSizeBytesReceived.Count < 4)
                    {
                        _windowSizeBytesReceived.Add(value);

                        if (_windowSizeBytesReceived.Count == 4)
                            _commandState = CommandState.None;
                    }
                    return true;

                default:
                    // Unhandled state
                    Debug.Assert(false);
                    _commandState = CommandState.None;
                    break;
            }

            // Not handled as command
            return false;
        }

        private void SetTerminalSelfEcho(bool enable)
        {
            // Enable:  Tell the terminal that it should should show typed characters itself (and that we won't echo them)
            // Disable: Tell the terminal that it should should NOT show typed characters itself (and that we will echo them if we want)
            SendBytes(new byte[] { IAC, enable ? WONT : WILL, OPTION_ECHO }, true);

            _echoToTerminal = !enable;
        }

        private void SetSuppressGoAhead()
        {
            // Tell the terminal that we don't want go-ahead characters from it
            SendBytes(new byte[] { IAC, DO, OPTION_SUPPRESS_GO_AHEAD }, true);

            // Tell the terminal that we will not be sending go-ahead characters
            SendBytes(new byte[] { IAC, WILL, OPTION_SUPPRESS_GO_AHEAD }, true);
        }

        private void SetWindowsSizeNegociation()
        {
            // Tell the terminal that we want to negociate window size
            SendBytes(new byte[] { IAC, DO, OPTION_NAWS }, true);
        }

        private void SendBytes(byte[] bytes, bool skipQueue)
        {
            try
            {
                if (skipQueue)
                {
                    lock (_stream)
                    {
                        _stream.WriteAsync(bytes, 0, bytes.Length, _workerTaskCancellation.Token).Wait();
                    }
                }
                else
                {
                    foreach (var b in bytes)
                        _bytesToSend.Enqueue(b);
                }
            }
            catch { }
        }
    }
}
