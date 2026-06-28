using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell;
using SoftShell.Telnet;

namespace TerminalInterfaceTest
{
    /// <summary>
    /// Integration tests for <see cref="TelnetTerminalInterface"/>. Each test sets up a real
    /// loopback TCP connection: the terminal interface runs on the server-side socket while the
    /// test plays the role of the Telnet client on the other end, exchanging raw bytes.
    /// </summary>
    public class TelnetTerminalInterfaceTests
    {
        // Telnet protocol bytes (mirrors the private constants in TelnetTerminalInterface).
        private const byte SE = 240, SB = 250, WILL = 251, WONT = 252, DO = 253, DONT = 254, IAC = 255;
        private const byte OPTION_ECHO = 1, OPTION_SUPPRESS_GO_AHEAD = 3, OPTION_NAWS = 31;

        /// <summary>
        /// A live loopback Telnet connection: a <see cref="TelnetTerminalInterface"/> bound to the
        /// server end and the client end exposed for the test to read from and write to. The option
        /// negotiation the terminal sends on start-up is consumed during construction and made
        /// available through <see cref="Negotiation"/>.
        /// </summary>
        private sealed class TelnetTestConnection : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly TcpClient _client;
            private readonly NetworkStream _clientStream;

            public TelnetTerminalInterface Terminal { get; }

            /// <summary>The option-negotiation bytes the terminal sent immediately after starting.</summary>
            public byte[] Negotiation { get; }

            public TelnetTestConnection()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

                _client = new TcpClient();
                _client.Connect(IPAddress.Loopback, port);
                _client.ReceiveTimeout = 5000;

                var serverSocket = _listener.AcceptSocket();

                Terminal = new TelnetTerminalInterface(serverSocket);
                Terminal.Start();

                _clientStream = _client.GetStream();

                // The terminal opens with four option requests (12 bytes); consume them so the
                // tests start from a clean stream.
                Negotiation = ReadBytes(12);
            }

            /// <summary>Reads exactly <paramref name="count"/> bytes from the client end.</summary>
            public byte[] ReadBytes(int count)
            {
                var buffer = new byte[count];
                _clientStream.ReadExactly(buffer, 0, count);
                return buffer;
            }

            /// <summary>Sends raw bytes from the client end to the terminal.</summary>
            public void Send(params byte[] bytes) => _clientStream.Write(bytes, 0, bytes.Length);

            /// <summary>Sends the ASCII bytes of <paramref name="text"/> from the client end.</summary>
            public void SendText(string text) => Send(Encoding.ASCII.GetBytes(text));

            public void Dispose()
            {
                try
                {
                    // Dispose blocks until the worker tasks stop; cap it so a hang fails the test
                    // rather than the whole run.
                    Task.Run(() => Terminal.Dispose()).Wait(TimeSpan.FromSeconds(10));
                }
                catch { }

                try { _client.Close(); } catch { }
                try { _listener.Stop(); } catch { }
            }
        }

        private static CancellationToken Timeout(int ms = 5000) => new CancellationTokenSource(ms).Token;

        /// <summary>Polls <paramref name="condition"/> until true or the timeout elapses.</summary>
        private static bool WaitFor(Func<bool> condition, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        [Fact]
        public void OnStart_SendsExpectedOptionNegotiation()
        {
            using var connection = new TelnetTestConnection();

            Assert.Equal(
                new byte[]
                {
                    IAC, DO,   OPTION_SUPPRESS_GO_AHEAD,
                    IAC, WILL, OPTION_SUPPRESS_GO_AHEAD,
                    IAC, WILL, OPTION_ECHO,
                    IAC, DO,   OPTION_NAWS,
                },
                connection.Negotiation);
        }

        [Fact]
        public void Properties_DescribeATelnetTerminal()
        {
            using var connection = new TelnetTestConnection();

            Assert.Equal("Telnet", connection.Terminal.TerminalType);
            Assert.Equal("\r\n", connection.Terminal.LineTermination);
            Assert.Equal(Encoding.ASCII, connection.Terminal.Encoding);
            Assert.False(string.IsNullOrEmpty(connection.Terminal.TerminalInstanceInfo));
        }

        [Fact]
        public void WindowSize_IsNullBeforeNawsNegotiation()
        {
            using var connection = new TelnetTestConnection();

            Assert.Null(connection.Terminal.WindowWidth);
            Assert.Null(connection.Terminal.WindowHeight);
        }

        [Fact]
        public void WindowSize_IsReportedFromNawsSubnegotiation()
        {
            using var connection = new TelnetTestConnection();

            // IAC SB NAWS <width-hi> <width-lo> <height-hi> <height-lo> IAC SE  ->  80 x 24
            connection.Send(IAC, SB, OPTION_NAWS, 0, 80, 0, 24, IAC, SE);

            Assert.True(WaitFor(() => connection.Terminal.WindowWidth == 80 && connection.Terminal.WindowHeight == 24),
                $"Expected 80x24 but got {connection.Terminal.WindowWidth}x{connection.Terminal.WindowHeight}");
        }

        [Fact]
        public async Task ReadLineAsync_ReturnsLineSentByClient()
        {
            using var connection = new TelnetTestConnection();

            connection.SendText("hello\r\n");

            var (strOut, action, _) = await connection.Terminal.ReadLineAsync(Timeout(), echo: false, initialString: null, isEscapingCheck: null);

            Assert.Equal("hello", strOut);
            Assert.Equal(KeyAction.None, action);
        }

        [Fact]
        public async Task ReadLineAsync_IgnoresInterleavedTelnetCommands()
        {
            using var connection = new TelnetTestConnection();

            // An option command in the middle of the typed text must not appear as input.
            connection.SendText("hi");
            connection.Send(IAC, DONT, OPTION_ECHO);
            connection.SendText("!\r\n");

            var (strOut, _, _) = await connection.Terminal.ReadLineAsync(Timeout(), echo: false, initialString: null, isEscapingCheck: null);

            Assert.Equal("hi!", strOut);
        }

        [Fact]
        public async Task WriteAsync_SendsTextToTheClient()
        {
            using var connection = new TelnetTestConnection();

            await connection.Terminal.WriteAsync("Hi", Timeout());

            var received = connection.ReadBytes(2);
            Assert.Equal("Hi", Encoding.ASCII.GetString(received));
        }

        [Fact]
        public void CtrlC_RaisesTaskCancelRequest()
        {
            using var connection = new TelnetTestConnection();
            using var cancelRequested = new ManualResetEventSlim(false);

            connection.Terminal.TaskCancelRequest += (_, _) => cancelRequested.Set();

            connection.Send(3); // ETX / Ctrl-C

            Assert.True(cancelRequested.Wait(5000));
        }
    }
}
