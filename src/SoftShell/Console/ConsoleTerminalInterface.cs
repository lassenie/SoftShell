using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Console
{
    /// <summary>
    /// A terminal interface for using the console.
    /// </summary>
    internal sealed class ConsoleTerminalInterface : TerminalInterface
    {
        private ConsoleColor _defaultColor = System.Console.ForegroundColor;

        /// <inheritdoc/>
        public override string TerminalType => "Console";

        /// <inheritdoc/>
        public override string TerminalInstanceInfo => "Local";

        /// <inheritdoc/>
        public override string LineTermination { get => Environment.NewLine; protected set { } }

        /// <inheritdoc/>
        public override Encoding Encoding { get => Encoding.UTF8; protected set { } }

        /// <inheritdoc/>
        public override int? WindowWidth => System.Console.WindowWidth;

        /// <inheritdoc/>
        public override int? WindowHeight => System.Console.WindowHeight;

        /// <summary>
        /// Constructor. Will be invoked by the <see cref="ConsoleTerminalListener"/>.
        /// </summary>
        public ConsoleTerminalInterface()
        {
            System.Console.InputEncoding = Encoding.UTF8;
            System.Console.OutputEncoding = Encoding.UTF8;

            System.Console.CancelKeyPress += Console_CancelKeyPress;
        }

        /// <inheritdoc/>
        public override Task FlushInputAsync(CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                while (System.Console.KeyAvailable && !cancelToken.IsCancellationRequested)
                    System.Console.ReadKey(true);
            });
        }

        /// <inheritdoc/>
        public override Task<IEnumerable<(KeyAction action, char character)>> TryReadAsync(bool echo)
        {
            return Task.Run(() =>
            {
                if (!System.Console.KeyAvailable)
                    return Enumerable.Empty<(KeyAction action, char character)>();

                var key = System.Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Backspace:
                        if (echo) System.Console.Write("\b \b");
                        return new[] { (KeyAction.Character, '\b') };

                    case ConsoleKey.Enter:
                        if (echo) System.Console.Write(LineTermination);
                        return LineTermination.Select(c => (KeyAction.Character, c)).ToList();

                    case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                        // Ctrl-C handling elsewhere using the Console.CancelKeyPress event
                        break;

                    case ConsoleKey.D when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                        return new[] { (KeyAction.Character, (char)4) }; // EOT character (EOF in Linux)

                    case ConsoleKey.Z when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                        return new[] { (KeyAction.Character, (char)26) }; // SUB character (EOF in Windows)

                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.RightArrow:
                        var isForward = (key.Key == ConsoleKey.RightArrow) ^ CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;

                        if (isForward)
                        {
                            if (echo) System.Console.Write(" "); // TODO: Something better to move cursor forward?
                            return new[] { (KeyAction.ArrowForward, '\0') };
                        }
                        else
                        {
                            if (echo) System.Console.Write("\b");
                            return new[] { (KeyAction.ArrowBack, '\0') };
                        }

                    case ConsoleKey.UpArrow:
                            return new[] { (KeyAction.ArrowUp, '\0') };

                    case ConsoleKey.DownArrow:
                        return new[] { (KeyAction.ArrowDown, '\0') };

                    case ConsoleKey.Home:
                        return new[] { (KeyAction.Home, '\0') };

                    case ConsoleKey.End:
                        return new[] { (KeyAction.End, '\0') };

                    default:
                        var ch = key.KeyChar;

                        if (!char.IsControl(ch))
                        {
                            if (echo) System.Console.Write(ch);
                            return new[] { (KeyAction.Character, ch) };
                        }
                        break;
                }

                return Enumerable.Empty<(KeyAction action, char character)>();
            });
        }

        /// <inheritdoc/>
        public override async Task<(string strOut, KeyAction escapingAction, char escapingChar)> ReadLineAsync(CancellationToken cancelToken, bool echo, string initialString, Func<KeyAction, char, bool> isEscapingCheck)
        {
            // Set up initial string
            string strOut = new string((initialString ?? string.Empty).Where(ch => !char.IsControl(ch) && ch >= 32).ToArray());
            int currentStrPos = strOut.Length;

            // Write initial string
            if (echo)
                System.Console.Write(strOut);

            while (!cancelToken.IsCancellationRequested)
            {
                var readData = await ReadAsync(cancelToken, false).ConfigureAwait(false);

                foreach (var data in readData)
                {
                    if (isEscapingCheck?.Invoke(data.action, data.character) ?? false)
                    {
                        // Clear string on screen
                        if (echo && (strOut.Length > 0))
                        {
                            if (currentStrPos > 0) System.Console.Write(string.Empty.PadRight(currentStrPos, '\b'));
                            System.Console.Write(string.Empty.PadRight(strOut.Length, ' '));
                            System.Console.Write(string.Empty.PadRight(strOut.Length, '\b'));
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
                                            System.Console.Write("\b");
                                            System.Console.Write(strOut.Substring(currentStrPos));
                                            System.Console.Write(" ");
                                            System.Console.Write(string.Empty.PadRight(strOut.Length - currentStrPos + 1, '\b'));
                                        }
                                    }
                                    break;

                                case '\r':
                                    break;

                                case '\n':
                                    if (echo) System.Console.WriteLine();
                                    return (strOut, KeyAction.None, '\0');

                                default:
                                    if (!char.IsControl(data.character))
                                    {
                                        strOut = strOut.Substring(0, currentStrPos) + data.character + strOut.Substring(currentStrPos);
                                        currentStrPos++;
                                        if (echo)
                                        {
                                            // Since inserting, write also remainder of the string at shifted position
                                            System.Console.Write(data.character + strOut.Substring(currentStrPos));

                                            // Move back to position inside the string?
                                            if (currentStrPos < strOut.Length)
                                                System.Console.Write(string.Empty.PadRight(strOut.Length - currentStrPos, '\b'));
                                        }
                                    }
                                    break;
                            }
                            break;

                        case KeyAction.ArrowForward:
                            if (currentStrPos < strOut.Length)
                            {
                                if (echo) System.Console.Write(strOut[currentStrPos]);
                                currentStrPos++;
                            }
                            break;

                        case KeyAction.ArrowBack:
                            if (currentStrPos > 0)
                            {
                                if (echo) System.Console.Write("\b");
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
                                if (echo) System.Console.Write(string.Empty.PadRight(currentStrPos, '\b'));
                                currentStrPos = 0;
                            }
                            break;

                        case KeyAction.End:
                            if (currentStrPos < strOut.Length)
                            {
                                if (echo) System.Console.Write(strOut.Substring(currentStrPos));
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
        }

        /// <inheritdoc/>
        public override Task WriteAsync(string text, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    System.Console.Write(text ?? string.Empty);
                }
                catch { }
            }, cancelToken);
        }

        /// <inheritdoc/>
        public override Task WriteLineAsync(string text, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    System.Console.WriteLine(text ?? string.Empty);
                }
                catch { }
            }, cancelToken);
        }

        /// <inheritdoc/>
        public override Task ClearScreenAsync(CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    System.Console.Clear();
                }
                catch { }
            }, cancelToken);
        }

        /// <inheritdoc/>
        public override Task SetTextColorAsync(ConsoleColor? color, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                switch (color)
                {
                    case ConsoleColor.Red:
                        System.Console.ForegroundColor = ConsoleColor.Red;
                        break;

                    case null:
                    default:
                        Debug.Assert(!color.HasValue);

                        System.Console.ForegroundColor = _defaultColor;
                        break;

                }

                CurrentTextColor = color;

            }, cancelToken);
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            RequestTaskCancel();
        }
    }
}
