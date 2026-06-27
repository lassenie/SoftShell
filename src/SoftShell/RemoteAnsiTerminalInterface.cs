using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell.Parsing;

namespace SoftShell
{
    /// <summary>
    /// Base class for terminal interfaces that communicate with a remote client over a
    /// byte-oriented connection (e.g. Telnet or SSH) using a limited ANSI terminal emulation.
    /// </summary>
    /// <remarks>
    /// This class provides the input/output logic shared by such terminals: queued input
    /// items, queued output bytes, ANSI escape sequence parsing of input, line editing and
    /// the ANSI sequences for writing text, clearing the screen and setting text color.
    /// Derived classes are responsible for the actual transport: feeding received bytes into
    /// <see cref="_receivedItems"/> (typically via <see cref="_ansiParser"/>) and flushing
    /// <see cref="_bytesToSend"/> to the connection.
    /// </remarks>
    public abstract class RemoteAnsiTerminalInterface : TerminalInterface
    {
        /// <summary>
        /// Cancels the derived class's worker task(s) when the terminal is disposed.
        /// </summary>
        protected readonly CancellationTokenSource _workerTaskCancellation = new CancellationTokenSource();

        /// <summary>
        /// Parsed input items (characters and key actions) waiting to be read by the session.
        /// </summary>
        protected readonly ConcurrentQueue<(KeyAction action, char character)> _receivedItems = new ConcurrentQueue<(KeyAction action, char character)>();

        /// <summary>
        /// Output bytes waiting to be sent to the terminal by the derived class's worker task.
        /// </summary>
        protected readonly ConcurrentQueue<byte> _bytesToSend = new ConcurrentQueue<byte>();

        /// <summary>
        /// Parses raw input bytes into characters and key actions (arrow keys etc.).
        /// </summary>
        protected readonly AnsiEscapeSequenceParser _ansiParser = new AnsiEscapeSequenceParser();

        /// <inheritdoc/>
        public override string LineTermination
        {
            get => "\r\n"; // Convention for a terminal in raw/character mode
            protected set { }
        }

        /// <inheritdoc/>
        public override Encoding Encoding
        {
            get => Encoding.ASCII; // Limited ANSI emulation uses plain ASCII
            protected set { }
        }

        /// <summary>
        /// The ANSI escape sequence sent to clear the screen and home the cursor.
        /// </summary>
        protected virtual string ClearScreenSequence => "\u001B[2J\u001B[H";

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
                    // ANSI character sequence for clearing the console.
                    var bytes = Encoding.GetBytes(ClearScreenSequence);

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
                            bytes = Encoding.GetBytes("\u001B[31m");
                            break;

                        case null:
                        default:
                            Debug.Assert(!color.HasValue);

                            // ANSI character sequence for resetting all text properties (default color).
                            // (For some reason Windows Telnet doesn't respond to code 39 (default color),
                            // so just reset all text properties.)
                            bytes = Encoding.GetBytes("\u001B[0m");
                            break;
                    }

                    foreach (var b in bytes)
                        _bytesToSend.Enqueue(b);
                }
                catch { }

                CurrentTextColor = color;

            }, cancelToken);
        }
    }
}
