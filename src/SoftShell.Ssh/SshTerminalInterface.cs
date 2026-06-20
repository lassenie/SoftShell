using FxSsh;
using FxSsh.Services;

using SoftShell.Parsing;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Ssh
{
    /// <summary>
    /// A terminal interface for communicating with SSH clients.
    /// Offers the same limited ANSI terminal emulation as the Telnet terminal interface.
    /// </summary>
    public class SshTerminalInterface : TerminalInterface
    {
        // The SSH session this terminal communicates through.
        private readonly Session _session;

        // "Stack" of channels in use. The latest (last) is the one currently communicated with.
        // A stack is kept so that nested channels can be popped back to a previous one when closed.
        private readonly List<SessionChannel> _channelStack = new List<SessionChannel>();

        // Cancels the output worker task when the terminal is disposed.
        private readonly CancellationTokenSource _workerTaskCancellation = new CancellationTokenSource();

        // Background task that flushes queued output bytes to the current channel.
        private Task? _writeTask;

        // Parsed input items (characters and key actions) waiting to be read by the session.
        private readonly ConcurrentQueue<(KeyAction action, char character)> _receivedItems = new ConcurrentQueue<(KeyAction action, char character)>();

        // Output bytes waiting to be sent to the terminal by the output worker task.
        private readonly ConcurrentQueue<byte> _bytesToSend = new ConcurrentQueue<byte>();

        // Parses raw input bytes into characters and key actions (arrow keys etc.).
        private readonly AnsiEscapeSequenceParser _ansiParser = new AnsiEscapeSequenceParser();

        // Terminal window size as last reported by the client (null until known).
        private int? _windowWidth = null;
        private int? _windowHeight = null;

        // True when the previous received input character was a CR, used to collapse CR-LF pairs.
        private bool _previousInputWasCr = false;

        /// <inheritdoc/>
        public override string TerminalType => "SSH";

        /// <inheritdoc/>
        public override string TerminalInstanceInfo => BitConverter.ToString(_session.SessionId);

        /// <inheritdoc/>
        public override string LineTermination
        {
            get => "\r\n"; // Convention for a terminal in raw/character mode
            protected set { }
        }

        /// <inheritdoc/>
        public override Encoding Encoding
        {
            get => Encoding.ASCII; // Same limited emulation as the Telnet interface
            protected set { }
        }

        /// <inheritdoc/>
        public override int? WindowWidth => _windowWidth;

        /// <inheritdoc/>
        public override int? WindowHeight => _windowHeight;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The SSH session to communicate through.</param>
        public SshTerminalInterface(Session session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            session.ServiceRegistered += Server_ServiceRegistered;

            // Worker task that flushes queued output bytes to the current channel.
            // Output produced before a channel exists (the session starts before the SSH
            // PTY channel is established) is kept queued until a channel is available.
            _writeTask = Task.Run(() =>
            {
                var buffer = new List<byte>();

                while (!_workerTaskCancellation.IsCancellationRequested)
                {
                    try
                    {
                        var channel = CurrentChannel;

                        // No channel yet: keep the queued bytes for when one becomes available.
                        if (channel == null)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        buffer.Clear();

                        while (_bytesToSend.TryDequeue(out var b))
                            buffer.Add(b);

                        if (buffer.Count > 0 && !channel.ClientClosed)
                        {
                            try { channel.SendData(buffer.ToArray()); }
                            catch { }
                        }

                        Thread.Sleep(50);
                    }
                    catch { }
                }
            });
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _workerTaskCancellation.Cancel();

            // Wait for the worker task to complete
            if (_writeTask != null)
            {
                while (!_writeTask.IsCompleted)
                    Thread.Sleep(100);
            }

            // Close all channels (latest first)
            foreach (var channel in ((IEnumerable<SessionChannel>)_channelStack).Reverse().ToArray())
                CloseChannel(channel);

            base.Dispose();
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

                // Drain everything currently available from the input queue.
                while (_receivedItems.TryDequeue(out var receivedItem))
                {
                    switch (receivedItem.action)
                    {
                        case KeyAction.Character:
                            switch (receivedItem.character)
                            {
                                // Backspace: erase the previous character on screen (destructive backspace).
                                case '\b':
                                    if (echo)
                                    {
                                        _bytesToSend.Enqueue((byte)'\b');
                                        _bytesToSend.Enqueue((byte)' ');
                                        _bytesToSend.Enqueue((byte)'\b');
                                    }
                                    output.Add(receivedItem);
                                    break;

                                // Line termination: passed through (and echoed) as-is.
                                case '\r':
                                case '\n':
                                    if (echo) _bytesToSend.Enqueue((byte)receivedItem.character); // Echo to terminal
                                    output.Add(receivedItem);
                                    break;

                                // Any other printable ASCII character.
                                default:
                                    if ((receivedItem.character >= ' ') && (receivedItem.character < 127))
                                    {
                                        if (echo) _bytesToSend.Enqueue((byte)receivedItem.character); // Echo to terminal
                                        output.Add(receivedItem);
                                    }
                                    break;
                            }
                            break;

                        // Non-character key actions (arrow keys, Home, End, ...) are never echoed.
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

                // Read and edit the line until Enter is pressed, an escaping key is hit,
                // or the operation is cancelled.
                while (!cancelToken.IsCancellationRequested)
                {
                    var readData = ReadAsync(cancelToken, false).Result;

                    foreach (var data in readData)
                    {
                        // Caller wants to handle this key itself (e.g. command history): abort the
                        // edit, wipe the current line from screen and hand the key back.
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
                                    // Backspace: remove the character before the cursor and
                                    // redraw the remainder of the line.
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

                                    // Bare CR is ignored; end-of-line is signalled by '\n'.
                                    case '\r':
                                        break;

                                    // Enter: echo a new line and return the completed line.
                                    case '\n':
                                        if (echo)
                                        {
                                            _bytesToSend.Enqueue((byte)'\r');
                                            _bytesToSend.Enqueue((byte)'\n');
                                        }
                                        return (strOut, KeyAction.None, '\0');

                                    // Any other (non-control) character: insert it at the cursor.
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

                            // Right arrow: move the cursor one character towards the end.
                            case KeyAction.ArrowForward:
                                if (currentStrPos < strOut.Length)
                                {
                                    if (echo) _bytesToSend.Enqueue((byte)strOut[currentStrPos]);
                                    currentStrPos++;
                                }
                                break;

                            // Left arrow: move the cursor one character towards the start.
                            case KeyAction.ArrowBack:
                                if (currentStrPos > 0)
                                {
                                    if (echo) _bytesToSend.Enqueue((byte)'\b');
                                    currentStrPos--;
                                }
                                break;

                            // Up/Down arrows are handled by the caller (command history) via the
                            // escaping check above, so nothing to do here.
                            case KeyAction.ArrowUp:
                            case KeyAction.ArrowDown:
                                // Do nothing
                                break;

                            // Home: move the cursor to the start of the line.
                            case KeyAction.Home:
                                if (currentStrPos > 0)
                                {
                                    if (echo) for (int i = 0; i < currentStrPos; i++) _bytesToSend.Enqueue((byte)'\b');
                                    currentStrPos = 0;
                                }
                                break;

                            // End: move the cursor to the end of the line.
                            case KeyAction.End:
                                if (currentStrPos < strOut.Length)
                                {
                                    if (echo) for (int i = currentStrPos; i < strOut.Length; i++) _bytesToSend.Enqueue((byte)strOut[i]);
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
                    // ANSI character sequence for clearing the console and homing the cursor.
                    var bytes = Encoding.GetBytes("[2J[H");

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
                            // ANSI character sequence for setting red text color.
                            bytes = Encoding.GetBytes("[31m");
                            break;

                        case null:
                        default:
                            Debug.Assert(!color.HasValue);

                            // ANSI character sequence for resetting all text properties (default color).
                            bytes = Encoding.GetBytes("[0m");
                            break;
                    }

                    foreach (var b in bytes)
                        _bytesToSend.Enqueue(b);
                }
                catch { }

                CurrentTextColor = color;

            }, cancelToken);
        }

        /// <summary>
        /// The channel currently in use for communication, or null if none is established yet.
        /// </summary>
        private SessionChannel? CurrentChannel
        {
            get
            {
                lock (_channelStack)
                {
                    return _channelStack.LastOrDefault();
                }
            }
        }

        /// <summary>
        /// Closes a channel, removes it from the stack and, if it was the active one,
        /// resumes communication on the previous channel (if any).
        /// </summary>
        /// <param name="channelToClose">The channel to close.</param>
        private void CloseChannel(SessionChannel? channelToClose)
        {
            lock (_channelStack)
            {
                // Close channel, if any
                if (channelToClose != null && _channelStack.Contains(channelToClose))
                {
                    bool wasLatest = (channelToClose == _channelStack.Last());

                    // Remove from the "stack" (also if not the latest)
                    _channelStack.Remove(channelToClose);

                    StopUsingChannel(channelToClose);

                    if (!channelToClose.ClientClosed)
                    {
                        try { channelToClose.SendClose(); }
                        catch { }
                    }

                    // If another channel is now latest, use that again
                    if (wasLatest)
                        UseLatestChannel();
                }
            }
        }

        /// <summary>
        /// Starts listening for data and close events on the latest (active) channel.
        /// </summary>
        private void UseLatestChannel()
        {
            if (_channelStack.Any())
            {
                _channelStack.Last().DataReceived += Channel_DataReceived;
                _channelStack.Last().CloseReceived += Channel_CloseReceived;
            }
        }

        /// <summary>
        /// Stops listening for data and close events on the given channel.
        /// </summary>
        /// <param name="channel">The channel to stop using.</param>
        private void StopUsingChannel(SessionChannel channel)
        {
            if (channel != null)
            {
                channel.DataReceived -= Channel_DataReceived;
                channel.CloseReceived -= Channel_CloseReceived;
            }
        }

        /// <summary>
        /// Handles SSH services becoming available on the session, hooking up the events
        /// needed for authentication, PTY allocation and window size changes.
        /// </summary>
        private void Server_ServiceRegistered(object? sender, SshService e)
        {
            if (!(sender is Session))
                return;

            if (e is UserauthService authService)
            {
                authService.Userauth += AuthService_UserAuth;
            }
            else if (e is ConnectionService connectionService)
            {
                connectionService.PtyReceived += ConnectionService_PtyReceived;
                connectionService.WindowChange += ConnectionService_WindowChange;
            }
        }

        /// <summary>
        /// Handles a client request for a pseudo terminal (PTY), making its channel the
        /// active one and recording the initial window size.
        /// </summary>
        private void ConnectionService_PtyReceived(object? sender, PtyArgs e)
        {
            lock (_channelStack)
            {
                // Pause using previous channel, if any
                if (_channelStack.Any())
                    StopUsingChannel(_channelStack.Last());

                // Add and use new channel
                _channelStack.Add(e.Channel);
                UseLatestChannel();
            }

            _windowWidth = (int)e.WidthChars;
            _windowHeight = (int)e.HeightRows;
        }

        /// <summary>
        /// Handles the client reporting a new terminal window size.
        /// </summary>
        private void ConnectionService_WindowChange(object? sender, WindowChangeArgs e)
        {
            _windowWidth = (int)e.WidthColumns;
            _windowHeight = (int)e.HeightRows;
        }

        /// <summary>
        /// Handles the client closing a channel.
        /// </summary>
        private void Channel_CloseReceived(object? sender, EventArgs e)
        {
            if (sender is SessionChannel channel)
                CloseChannel(channel);
        }

        /// <summary>
        /// Handles raw data received from the client: parses it into characters and key
        /// actions, normalizes line endings and queues the result for reading.
        /// </summary>
        private void Channel_DataReceived(object? sender, byte[] e)
        {
            foreach (var b in e)
            {
                foreach (var item in _ansiParser.HandleByte(b))
                {
                    var action = item.action;
                    var character = item.character;

                    if (action == KeyAction.Character)
                    {
                        // SSH terminals send a bare CR for the Enter key. Treat CR as line
                        // termination ('\n') and collapse an immediately following LF, so that
                        // both CR and CR-LF result in a single end-of-line (like the Telnet
                        // interface, whose clients send CR-LF).
                        if (character == '\r')
                        {
                            _previousInputWasCr = true;
                            character = '\n';
                        }
                        else if (character == '\n' && _previousInputWasCr)
                        {
                            _previousInputWasCr = false;
                            continue;
                        }
                        else
                        {
                            _previousInputWasCr = false;
                        }

                        // Ctrl-C pressed?
                        if (character == (char)3)
                        {
                            RequestTaskCancel();
                            continue;
                        }
                    }
                    else
                    {
                        _previousInputWasCr = false;
                    }

                    _receivedItems.Enqueue((action, character));
                }
            }
        }

        /// <summary>
        /// Handles SSH-level user authentication. This is accepted unconditionally; the
        /// actual SoftShell authorization happens through user interaction on the terminal.
        /// </summary>
        /// <remarks>
        /// The connection is still encrypted (the SSH key exchange happens before
        /// authentication), but any SSH credentials are accepted, so access control is
        /// entirely up to the SoftShell session running on top of this terminal.
        /// </remarks>
        private void AuthService_UserAuth(object? sender, UserauthArgs e)
        {
            // Just consider authorized at this point - actual SoftShell authorization
            // will happen through user interaction on the terminal.
            e.Result = true;
        }
    }
}
