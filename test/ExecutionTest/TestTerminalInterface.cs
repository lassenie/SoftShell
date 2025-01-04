using SoftShell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest
{
    public class TestTerminalInterface : TerminalInterface
    {
        private Queue<(KeyAction action, char character)> _inputQueue = new Queue<(KeyAction action, char character)>();
        private StringBuilder _writtenText = new StringBuilder();

        public bool IsCommandChainStarted { get; set; } = false;
        public bool IsCommandChainEnded { get; set; } = false;

        public override string TerminalType => "Test";

        public override string TerminalInstanceInfo => "Test terminal";

        public override string LineTermination
        {
            get => "\r\n";
            protected set { }
        }

        public override Encoding Encoding
        {
            get => Encoding.UTF8;
            protected set { }
        }

        public override int? WindowWidth => 80;

        public override int? WindowHeight => 25;

        internal string WrittenText => _writtenText.ToString();

        internal void ReceiveLine(string text)
        {
            foreach (var ch in text)
                _inputQueue.Enqueue((KeyAction.Character, ch));

            foreach (var ch in LineTermination)
                _inputQueue.Enqueue((KeyAction.Character, ch));
        }

        internal void ClearWrittenText()
        {
            _writtenText.Clear();
        }

        public override Task FlushInputAsync(CancellationToken cancelToken)
        {
            _inputQueue.Clear();
            return Task.CompletedTask;
        }

        public override Task<IEnumerable<(KeyAction action, char character)>> TryReadAsync(bool echo)
        {
            if (_inputQueue.TryDequeue(out var input))
                return Task.FromResult(new (KeyAction action, char character)[] { input }.AsEnumerable());
            else
                return Task.FromResult(Enumerable.Empty<(KeyAction action, char character)>());
        }

        public override Task<(string strOut, KeyAction escapingAction, char escapingChar)> ReadLineAsync(CancellationToken cancelToken, bool echo, string initialString, Func<KeyAction, char, bool> isEscapingCheck)
        {
            return Task.Run(() =>
            {
                // Set up initial string
                string strOut = new string((initialString ?? string.Empty).Where(ch => !char.IsControl(ch) && ch >= 32).ToArray());
                int currentStrPos = strOut.Length;

                while (!cancelToken.IsCancellationRequested)
                {
                    var readData = ReadAsync(cancelToken, false).Result;

                    foreach (var data in readData)
                    {
                        if (isEscapingCheck?.Invoke(data.action, data.character) ?? false)
                        {
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
                                        }
                                        break;

                                    case '\r':
                                        break;

                                    case '\n':
                                        return (strOut, KeyAction.None, '\0');

                                    default:
                                        if (!char.IsControl(data.character))
                                        {
                                            strOut = strOut.Substring(0, currentStrPos) + data.character + strOut.Substring(currentStrPos);
                                            currentStrPos++;
                                        }
                                        break;
                                }
                                break;

                            case KeyAction.ArrowForward:
                                if (currentStrPos < strOut.Length)
                                {
                                    currentStrPos++;
                                }
                                break;

                            case KeyAction.ArrowBack:
                                if (currentStrPos > 0)
                                {
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
                                    currentStrPos = 0;
                                }
                                break;

                            case KeyAction.End:
                                if (currentStrPos < strOut.Length)
                                {
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

                return (string.Empty, KeyAction.None, '\0');
            });
        }

        public override Task WriteAsync(string text, CancellationToken cancelToken)
        {
            if (IsCommandChainStarted && !IsCommandChainEnded)
                _writtenText.Append(text);

            return Task.CompletedTask;
        }

        public override Task ClearScreenAsync(CancellationToken cancelToken)
        {
            if (IsCommandChainStarted && !IsCommandChainEnded)
                _writtenText.Append("(ClearScreen)");

            return Task.CompletedTask;
        }

        public override Task SetTextColorAsync(ConsoleColor? color, CancellationToken cancelToken)
        {
            if (IsCommandChainStarted && !IsCommandChainEnded)
            {
                _writtenText.Append($"(TextColor:{(color?.ToString() ?? "Default")})");
                CurrentTextColor = color;
            }

            return Task.CompletedTask;
        }

        public override Task ReportCommandChainBeginningOfOutputAsync()
        {
            IsCommandChainStarted = true;
            return Task.CompletedTask;
        }

        public override Task ReportCommandChainEndOfOutputAsync()
        {
            IsCommandChainEnded = true;
            return Task.CompletedTask;
        }
    }

    public class TestTerminalListener : ITerminalListener
    {
        private TestTerminalInterface _terminalInterface;

        public event TerminalConnectedHandler? TerminalConnected;

        public TestTerminalListener(TestTerminalInterface terminalInterface)
        {
            _terminalInterface = terminalInterface;
        }

        public void Dispose()
        {
        }

        public Task RunAsync(ISessionCreator sessionCreator)
        {
            var session = sessionCreator.CreateSession(_terminalInterface);
            TerminalConnected?.Invoke(this, new TerminalConnectedEventArgs(session));

            return Task.CompletedTask;
        }
    }

}
