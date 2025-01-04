using SoftShell;
using SoftShell.Parsing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

namespace SoftShell.Telnet
{
    /// <summary>
    /// A terminal interface for communicating with Telnet clients.
    /// </summary>
    internal sealed class TelnetTerminalInterface : TerminalInterface
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
        private CancellationTokenSource _workerTaskCancellation = new CancellationTokenSource();
        private Task _writeTask;
        private Task _readTask;
        private CommandState _commandState = CommandState.None;
        private bool _echoToTerminal = false;
        private ConcurrentQueue<(KeyAction action, char character)> _receivedItems = new ConcurrentQueue<(KeyAction action, char character)>();
        private ConcurrentQueue<byte> _bytesToSend   = new ConcurrentQueue<byte>();

        private List<byte> _windowSizeBytesReceived = new List<byte>();

        private AnsiEscapeSequenceParser _ansiParser = new AnsiEscapeSequenceParser();

        /// <inheritdoc/>
        public override string TerminalType => "Telnet";

        /// <inheritdoc/>
        public override string TerminalInstanceInfo => _socket.RemoteEndPoint.ToString();

        /// <inheritdoc/>
        public override string LineTermination
        {
            get => "\r\n"; // Telnet convention
            protected set { }
        }

        /// <inheritdoc/>
        public override Encoding Encoding
        {
            get => Encoding.ASCII; // Normal for Telnet
            protected set { }
        }

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

        /// <inheritdoc/>
        public override Task FlushInputAsync(CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                while (!cancelToken.IsCancellationRequested &&
                       _receivedItems.TryDequeue(out var _))
                    Thread.Sleep(0);
            });
        }

        /// <inheritdoc/>
        public override Task<IEnumerable<(KeyAction action, char character)>> TryReadAsync(bool echo)
        {
            return Task.Run(() =>
            {
                var output = new List<(KeyAction action, char character)>();

                while (_receivedItems.TryDequeue(out var receivedItem))
                {
                    switch (receivedItem.action)
                    {
                        case KeyAction.Character:
                            switch (receivedItem.character)
                            {
                                case '\b':
                                    if (echo)
                                    {
                                        _bytesToSend.Enqueue((byte)'\b');
                                        _bytesToSend.Enqueue((byte)' ');
                                        _bytesToSend.Enqueue((byte)'\b');
                                    }
                                    output.Add(receivedItem);
                                    break;

                                case '\r':
                                case '\n':
                                    if (echo) _bytesToSend.Enqueue((byte)receivedItem.character); // Echo to terminal
                                    output.Add(receivedItem);
                                    break;

                                default:
                                    if ((receivedItem.character >= ' ') && (receivedItem.character < 127))
                                    {
                                        if (echo) _bytesToSend.Enqueue((byte)receivedItem.character); // Echo to terminal
                                        output.Add(receivedItem);
                                    }
                                    break;
                            }
                            break;

                        default:
                            output.Add(receivedItem);
                            break;
                    }
                }

                return output.AsEnumerable();
            });
        }

        /// <inheritdoc/>
        public override Task<(string strOut, KeyAction escapingAction, char escapingChar)> ReadLineAsync(CancellationToken cancelToken, bool echo, string initialString, Func<KeyAction, char, bool> isEscapingCheck)
        {
            return Task.Run(() =>
            {
                // Set up initial string
                string strOut = new string((initialString ?? string.Empty).Where(ch => !char.IsControl(ch) && ch >= 32 && ch < 127).ToArray());
                int currentStrPos = strOut.Length;

                // Write initial string
                if (echo)
                {
                    foreach (var ch in strOut)
                        _bytesToSend.Enqueue((byte)ch);
                }

                while (!cancelToken.IsCancellationRequested)
                {
                    var readData = ReadAsync(cancelToken, false).Result;

                    foreach (var data in readData)
                    {
                        if (isEscapingCheck?.Invoke(data.action, data.character) ?? false)
                        {
                            // Clear string on screen
                            if (echo)
                            {
                                for (int i = 0; i < currentStrPos; i++) _bytesToSend.Enqueue((byte)'\b');
                                for (int i = 0; i < strOut.Length; i++) _bytesToSend.Enqueue((byte)' ');
                                for (int i = 0; i < strOut.Length; i++) _bytesToSend.Enqueue((byte)'\b');
                            }

                            return (string.Empty, data.action, data.character);
                        }

                        switch (data.action)
                        {
                            case KeyAction.Character:
                                switch (data.character)
                                {
                                    case '\b':
                                        if (currentStrPos > 0)
                                        {
                                            strOut = strOut.Remove(currentStrPos - 1, 1);
                                            currentStrPos--;
                                            if (echo)
                                            {
                                                _bytesToSend.Enqueue((byte)'\b');
                                                for (int i = currentStrPos; i < strOut.Length; i++) _bytesToSend.Enqueue((byte)strOut[i]);
                                                _bytesToSend.Enqueue((byte)' ');
                                                for (int i = 0; i <= strOut.Length - currentStrPos; i++) _bytesToSend.Enqueue((byte)'\b');
                                            }


                                        }
                                        break;

                                    case '\r':
                                        break;

                                    case '\n':
                                        if (echo)
                                        {
                                            _bytesToSend.Enqueue((byte)'\r');
                                            _bytesToSend.Enqueue((byte)'\n');
                                        }
                                        return (strOut, KeyAction.None, '\0');

                                    default:
                                        if (!char.IsControl(data.character))
                                        {
                                            strOut = strOut.Substring(0, currentStrPos) + data.character + strOut.Substring(currentStrPos);
                                            if (echo)
                                            {
                                                // Insert mode: Write the character and push the subsequent characters
                                                for (int i = currentStrPos; i < strOut.Length; i++) _bytesToSend.Enqueue((byte)strOut[i]);
                                                for (int i = currentStrPos; i < strOut.Length - 1; i++) _bytesToSend.Enqueue((byte)'\b');
                                            }
                                            currentStrPos++;
                                        }
                                        break;
                                }
                                break;

                            case KeyAction.ArrowForward:
                                if (currentStrPos < strOut.Length)
                                {
                                    if (echo) _bytesToSend.Enqueue((byte)strOut[currentStrPos]);
                                    currentStrPos++;
                                }
                                break;

                            case KeyAction.ArrowBack:
                                if (currentStrPos > 0)
                                {
                                    if (echo) _bytesToSend.Enqueue((byte)'\b');
                                    currentStrPos--;
                                }
                                break;

                            case KeyAction.ArrowUp:
                            case KeyAction.ArrowDown:
                                // Do nothing
                                break;

                            case KeyAction.Home:
                                if (currentStrPos > 0)
                                {
                                    if (echo) for (int i = 0; i < currentStrPos; i++) _bytesToSend.Enqueue((byte)'\b');
                                    currentStrPos = 0;
                                }
                                break;

                            case KeyAction.End:
                                if (currentStrPos < strOut.Length)
                                {
                                    if (echo) for (int i = currentStrPos; i < strOut.Length; i++)  _bytesToSend.Enqueue((byte)strOut[i]);
                                    currentStrPos = strOut.Length;
                                }
                                break;

                            default:
                                // Unhandled action
                                Debug.Assert(false);
                                break;
                        }
                    }
                }

                throw new TaskCanceledException();
            });
        }

        /// <inheritdoc/>
        public override Task WriteAsync(string text, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                // Get bytes to transmit. Replace non-ASCII characters with '?'.
                var bytes = Encoding.GetBytes(text.Select(ch => (ch >= 0 && ch < 127) ? ch : '?').ToArray());

                foreach (var b in bytes)
                    _bytesToSend.Enqueue(b);
            });
        }

        /// <inheritdoc/>
        public override Task ClearScreenAsync(CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Get bytes to transmit for ANSI character sequence for clearing the console.
                    var bytes = Encoding.GetBytes("\u001B[2J");

                    foreach (var b in bytes)
                        _bytesToSend.Enqueue(b);
                }
                catch { }
            }, cancelToken);
        }

        /// <inheritdoc/>
        public override Task SetTextColorAsync(ConsoleColor? color, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    byte[] bytes;

                    switch (color)
                    {
                        case ConsoleColor.Red:
                            // Get bytes to transmit for ANSI character sequence for setting red text color.
                            bytes = Encoding.GetBytes("\u001B[31m");
                            break;

                        case null:
                        default:
                            Debug.Assert(!color.HasValue);

                            // Get bytes to transmit for ANSI character sequence for setting default text color.
                            bytes = Encoding.GetBytes("\u001B[0m"); // For some reason Windows Telnet doesn't respond to code 39 (default color), so just reset all text properties
                            break;
                    }

                    foreach (var b in bytes)
                        _bytesToSend.Enqueue(b);
                }
                catch { }

                CurrentTextColor = color;

            }, cancelToken);
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
