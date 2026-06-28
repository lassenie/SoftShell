using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell;
using SoftShell.Ssh;

using Session = FxSsh.Session;

namespace TerminalInterfaceTest
{
    /// <summary>
    /// Tests for <see cref="SshTerminalInterface"/>. The interface is constructed around a real
    /// FxSsh <see cref="Session"/> backed by a loopback socket, but no SSH handshake is performed:
    /// that keeps the tests deterministic while still exercising the actual SSH terminal type
    /// (its properties and the ANSI input/output logic it inherits from the remote-terminal base).
    /// Behaviour that depends on a negotiated PTY channel (window size, the channel data path) is
    /// out of scope here and is covered indirectly by the remote-terminal and ANSI parser tests.
    /// </summary>
    public class SshTerminalInterfaceTests
    {
        /// <summary>
        /// Test double that exposes the protected input/output queues of the SSH terminal so input
        /// can be injected and queued output inspected without a live channel.
        /// </summary>
        private sealed class TestSshTerminalInterface : SshTerminalInterface
        {
            public TestSshTerminalInterface(Session session) : base(session) { }

            public void Receive(KeyAction action, char character) => _receivedItems.Enqueue((action, character));

            public void ReceiveText(string text)
            {
                foreach (var ch in text)
                    Receive(KeyAction.Character, ch);
            }

            public byte[] DrainSentBytes()
            {
                var bytes = new List<byte>();
                while (_bytesToSend.TryDequeue(out var b))
                    bytes.Add(b);
                return bytes.ToArray();
            }

            public string DrainSentText() => Encoding.ASCII.GetString(DrainSentBytes());
        }

        /// <summary>
        /// Sets up a loopback socket pair, wraps the server end in an FxSsh session (no handshake)
        /// and builds an SSH terminal interface around it.
        /// </summary>
        private sealed class SshTestConnection : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly TcpClient _client;
            private readonly Socket _serverSocket;

            public TestSshTerminalInterface Terminal { get; }

            public SshTestConnection()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

                _client = new TcpClient();
                _client.Connect(IPAddress.Loopback, port);
                _serverSocket = _listener.AcceptSocket();

                var session = new Session(_serverSocket, new Dictionary<string, string>(), "SSH-2.0-SoftShellTest");
                Terminal = new TestSshTerminalInterface(session);
            }

            public void Dispose()
            {
                try { Task.Run(() => Terminal.Dispose()).Wait(TimeSpan.FromSeconds(10)); } catch { }
                try { _client.Close(); } catch { }
                try { _serverSocket.Close(); } catch { }
                try { _listener.Stop(); } catch { }
            }
        }

        private static CancellationToken Timeout(int ms = 5000) => new CancellationTokenSource(ms).Token;

        [Fact]
        public void Properties_DescribeAnSshTerminal()
        {
            using var connection = new SshTestConnection();

            Assert.Equal("SSH", connection.Terminal.TerminalType);
            Assert.Equal("\r\n", connection.Terminal.LineTermination);
            Assert.Equal(Encoding.ASCII, connection.Terminal.Encoding);
        }

        [Fact]
        public void WindowSize_IsNullBeforeAnyPtyRequest()
        {
            using var connection = new SshTestConnection();

            Assert.Null(connection.Terminal.WindowWidth);
            Assert.Null(connection.Terminal.WindowHeight);
        }

        [Fact]
        public async Task WriteAsync_QueuesAsciiBytes()
        {
            using var connection = new SshTestConnection();

            await connection.Terminal.WriteAsync("AB", Timeout());

            Assert.Equal("AB", connection.Terminal.DrainSentText());
        }

        [Fact]
        public async Task WriteAsync_ReplacesNonAsciiWithQuestionMark()
        {
            using var connection = new SshTestConnection();

            await connection.Terminal.WriteAsync("é", Timeout());

            Assert.Equal("?", connection.Terminal.DrainSentText());
        }

        [Fact]
        public async Task ClearScreenAsync_QueuesAnsiClearAndHomeSequence()
        {
            using var connection = new SshTestConnection();

            await connection.Terminal.ClearScreenAsync(Timeout());

            Assert.Equal("[2J[H", connection.Terminal.DrainSentText());
        }

        [Fact]
        public async Task SetTextColorAsync_QueuesAnsiColorSequencesAndTracksColor()
        {
            using var connection = new SshTestConnection();

            await connection.Terminal.SetTextColorAsync(ConsoleColor.Red, Timeout());
            Assert.Equal("[31m", connection.Terminal.DrainSentText());
            Assert.Equal(ConsoleColor.Red, connection.Terminal.CurrentTextColor);

            await connection.Terminal.SetTextColorAsync(null, Timeout());
            Assert.Equal("[0m", connection.Terminal.DrainSentText());
            Assert.Null(connection.Terminal.CurrentTextColor);
        }

        [Fact]
        public async Task TryReadAsync_ReturnsAndEchoesPrintableCharacter()
        {
            using var connection = new SshTestConnection();
            connection.Terminal.Receive(KeyAction.Character, 'A');

            var read = (await connection.Terminal.TryReadAsync(echo: true)).ToList();

            Assert.Equal(new[] { (KeyAction.Character, 'A') }, read);
            Assert.Equal("A", connection.Terminal.DrainSentText());
        }

        [Fact]
        public async Task ReadLineAsync_ReturnsLineFromQueuedInput()
        {
            using var connection = new SshTestConnection();
            connection.Terminal.ReceiveText("hi");
            connection.Terminal.Receive(KeyAction.Character, '\n');

            var (strOut, action, _) = await connection.Terminal.ReadLineAsync(Timeout(), echo: false, initialString: null, isEscapingCheck: null);

            Assert.Equal("hi", strOut);
            Assert.Equal(KeyAction.None, action);
        }

        [Fact]
        public async Task FlushInputAsync_DiscardsQueuedInput()
        {
            using var connection = new SshTestConnection();
            connection.Terminal.ReceiveText("discarded");

            await connection.Terminal.FlushInputAsync(Timeout());

            var read = (await connection.Terminal.TryReadAsync(echo: false)).ToList();
            Assert.Empty(read);
        }

        [Fact]
        public void ConstructionAndDisposal_DoNotThrow()
        {
            var connection = new SshTestConnection();
            connection.Dispose();
        }
    }
}
