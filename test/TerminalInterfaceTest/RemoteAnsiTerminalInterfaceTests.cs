using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell;

namespace TerminalInterfaceTest
{
    /// <summary>
    /// Tests for <see cref="RemoteAnsiTerminalInterface"/>, the base class that provides the
    /// input reading, line editing and ANSI output logic shared by the Telnet and SSH terminal
    /// interfaces. A minimal subclass exposes the protected input/output queues so the behaviour
    /// can be driven without a real network connection.
    /// </summary>
    public class RemoteAnsiTerminalInterfaceTests
    {
        /// <summary>
        /// Test double that exposes the protected queues of <see cref="RemoteAnsiTerminalInterface"/>
        /// and supplies the few members the base class leaves abstract.
        /// </summary>
        private sealed class TestRemoteAnsiTerminal : RemoteAnsiTerminalInterface
        {
            public override string TerminalType => "TestRemote";
            public override string TerminalInstanceInfo => "TestRemoteInstance";
            public override int? WindowWidth => 80;
            public override int? WindowHeight => 25;

            /// <summary>Simulates a byte/key item arriving from the remote client.</summary>
            public void Receive((KeyAction action, char character) item) => _receivedItems.Enqueue(item);

            public void Receive(KeyAction action, char character) => Receive((action, character));

            /// <summary>Simulates printable text arriving, character by character.</summary>
            public void ReceiveText(string text)
            {
                foreach (var ch in text)
                    Receive(KeyAction.Character, ch);
            }

            /// <summary>Drains and returns everything currently queued for sending to the client.</summary>
            public byte[] DrainSentBytes()
            {
                var bytes = new List<byte>();
                while (_bytesToSend.TryDequeue(out var b))
                    bytes.Add(b);
                return bytes.ToArray();
            }

            public string DrainSentText() => Encoding.ASCII.GetString(DrainSentBytes());
        }

        private static CancellationToken Timeout(int ms = 5000) => new CancellationTokenSource(ms).Token;

        [Fact]
        public void Properties_MatchRemoteAnsiConventions()
        {
            using var terminal = new TestRemoteAnsiTerminal();

            Assert.Equal("\r\n", terminal.LineTermination);
            Assert.Equal(Encoding.ASCII, terminal.Encoding);
        }

        [Fact]
        public async Task TryReadAsync_ReturnsAndEchoesPrintableCharacter()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.Receive(KeyAction.Character, 'A');

            var read = (await terminal.TryReadAsync(echo: true)).ToList();

            Assert.Equal(new[] { (KeyAction.Character, 'A') }, read);
            Assert.Equal("A", terminal.DrainSentText());
        }

        [Fact]
        public async Task TryReadAsync_EchoesBackspaceDestructively()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.Receive(KeyAction.Character, '\b');

            var read = (await terminal.TryReadAsync(echo: true)).ToList();

            Assert.Equal(new[] { (KeyAction.Character, '\b') }, read);
            // Destructive backspace: move back, overwrite with space, move back again.
            Assert.Equal("\b \b", terminal.DrainSentText());
        }

        [Fact]
        public async Task TryReadAsync_WithoutEcho_SendsNothing()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("Hi");

            var read = (await terminal.TryReadAsync(echo: false)).ToList();

            Assert.Equal(new[] { (KeyAction.Character, 'H'), (KeyAction.Character, 'i') }, read);
            Assert.Empty(terminal.DrainSentBytes());
        }

        [Fact]
        public async Task TryReadAsync_DropsNonPrintableControlCharacters()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.Receive(KeyAction.Character, (char)1); // SOH - not printable, not handled specially
            terminal.Receive(KeyAction.Character, 'X');

            var read = (await terminal.TryReadAsync(echo: true)).ToList();

            Assert.Equal(new[] { (KeyAction.Character, 'X') }, read);
            Assert.Equal("X", terminal.DrainSentText());
        }

        [Fact]
        public async Task TryReadAsync_PassesThroughNonCharacterActionsWithoutEcho()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.Receive(KeyAction.ArrowUp, '\0');

            var read = (await terminal.TryReadAsync(echo: true)).ToList();

            Assert.Equal(new[] { (KeyAction.ArrowUp, '\0') }, read);
            Assert.Empty(terminal.DrainSentBytes());
        }

        [Fact]
        public async Task FlushInputAsync_DiscardsQueuedInput()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("discarded");

            await terminal.FlushInputAsync(Timeout());

            var read = (await terminal.TryReadAsync(echo: false)).ToList();
            Assert.Empty(read);
        }

        [Fact]
        public async Task ReadLineAsync_ReturnsTypedLineUpToEnter()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("hello");
            terminal.Receive(KeyAction.Character, '\n');

            var (strOut, action, ch) = await terminal.ReadLineAsync(Timeout(), echo: false, initialString: null, isEscapingCheck: null);

            Assert.Equal("hello", strOut);
            Assert.Equal(KeyAction.None, action);
            Assert.Equal('\0', ch);
        }

        [Fact]
        public async Task ReadLineAsync_StartsFromInitialString()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("def");
            terminal.Receive(KeyAction.Character, '\n');

            var (strOut, _, _) = await terminal.ReadLineAsync(Timeout(), echo: false, initialString: "abc", isEscapingCheck: null);

            Assert.Equal("abcdef", strOut);
        }

        [Fact]
        public async Task ReadLineAsync_BackspaceRemovesPreviousCharacter()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("abc");
            terminal.Receive(KeyAction.Character, '\b'); // delete 'c'
            terminal.Receive(KeyAction.Character, 'X');
            terminal.Receive(KeyAction.Character, '\n');

            var (strOut, _, _) = await terminal.ReadLineAsync(Timeout(), echo: false, initialString: null, isEscapingCheck: null);

            Assert.Equal("abX", strOut);
        }

        [Fact]
        public async Task ReadLineAsync_HomeAndArrowAllowInsertionInsideLine()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("bc");
            terminal.Receive(KeyAction.Home, '\0');         // cursor to start
            terminal.Receive(KeyAction.Character, 'a');     // insert before 'b' -> "abc"
            terminal.Receive(KeyAction.End, '\0');          // cursor to end
            terminal.Receive(KeyAction.Character, 'd');     // append -> "abcd"
            terminal.Receive(KeyAction.Character, '\n');

            var (strOut, _, _) = await terminal.ReadLineAsync(Timeout(), echo: false, initialString: null, isEscapingCheck: null);

            Assert.Equal("abcd", strOut);
        }

        [Fact]
        public async Task ReadLineAsync_EscapingKeyAbortsAndReportsTheKey()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("xy");
            terminal.Receive(KeyAction.ArrowUp, '\0'); // treated as escaping (e.g. command history)

            var (strOut, action, ch) = await terminal.ReadLineAsync(
                Timeout(),
                echo: false,
                initialString: null,
                isEscapingCheck: (a, c) => a == KeyAction.ArrowUp);

            Assert.Equal(string.Empty, strOut);
            Assert.Equal(KeyAction.ArrowUp, action);
            Assert.Equal('\0', ch);
        }

        [Fact]
        public async Task ReadLineAsync_WithEcho_WritesTypedCharactersAndNewLine()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            terminal.ReceiveText("hi");
            terminal.Receive(KeyAction.Character, '\n');

            await terminal.ReadLineAsync(Timeout(), echo: true, initialString: null, isEscapingCheck: null);

            // 'h', 'i' echoed, then CR-LF for the Enter key.
            Assert.Equal("hi\r\n", terminal.DrainSentText());
        }

        [Fact]
        public async Task WriteAsync_ReplacesNonAsciiCharactersWithQuestionMark()
        {
            using var terminal = new TestRemoteAnsiTerminal();

            await terminal.WriteAsync("hééo", Timeout());

            Assert.Equal("h??o", terminal.DrainSentText());
        }

        [Fact]
        public async Task WriteLineAsync_AppendsLineTermination()
        {
            using var terminal = new TestRemoteAnsiTerminal();

            await terminal.WriteLineAsync("done", Timeout());

            Assert.Equal("done\r\n", terminal.DrainSentText());
        }

        [Fact]
        public async Task ClearScreenAsync_SendsAnsiClearAndHomeSequence()
        {
            using var terminal = new TestRemoteAnsiTerminal();

            await terminal.ClearScreenAsync(Timeout());

            Assert.Equal("[2J[H", terminal.DrainSentText());
        }

        [Fact]
        public async Task SetTextColorAsync_Red_SendsRedSequenceAndTracksColor()
        {
            using var terminal = new TestRemoteAnsiTerminal();

            await terminal.SetTextColorAsync(ConsoleColor.Red, Timeout());

            Assert.Equal("[31m", terminal.DrainSentText());
            Assert.Equal(ConsoleColor.Red, terminal.CurrentTextColor);
        }

        [Fact]
        public async Task SetTextColorAsync_Default_SendsResetSequenceAndClearsColor()
        {
            using var terminal = new TestRemoteAnsiTerminal();
            await terminal.SetTextColorAsync(ConsoleColor.Red, Timeout());
            terminal.DrainSentBytes();

            await terminal.SetTextColorAsync(null, Timeout());

            Assert.Equal("[0m", terminal.DrainSentText());
            Assert.Null(terminal.CurrentTextColor);
        }
    }
}
