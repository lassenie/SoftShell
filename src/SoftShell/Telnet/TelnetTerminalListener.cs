using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

namespace SoftShell.Telnet
{
    /// <summary>
    /// Listens for incoming Telnet connections and instantiates a <see cref="TelnetTerminalInterface"/> for each.
    /// </summary>
    public sealed class TelnetTerminalListener : ITerminalListener
    {
        private Socket _listener;
        private Task _serverTask;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// IP end point for incoming Telnet connections.
        /// </summary>
        public IPEndPoint IPEndPoint { get; private set; }

        /// <inheritdoc/>
        public event TerminalConnectedHandler TerminalConnected;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">IP address for incoming Telnet connections.</param>
        /// <param name="port">TCP port for incoming Telnet connections.</param>
        public TelnetTerminalListener(long address, int port) : this(new IPAddress(address), port) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">IP address for incoming Telnet connections.</param>
        /// <param name="port">TCP port for incoming Telnet connections.</param>
        public TelnetTerminalListener(IPAddress ipAddress, int port)
        {
            IPEndPoint = new IPEndPoint(ipAddress, port);
        }

        /// <inheritdoc/>
        public Task RunAsync(ISessionCreator sessionCreator)
        {
            _listener = new Socket(IPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _listener.Bind(IPEndPoint);
            _listener.Listen(1);

            return _serverTask = Task.Run(() => Listen(sessionCreator));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _listener.Dispose();
        }

        private async void Listen(ISessionCreator sessionCreator)
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var socket = await _listener.AcceptAsync();
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    var terminal = new TelnetTerminalInterface(socket);

                    terminal.Start();

                    TerminalConnected?.Invoke(this, new TerminalConnectedEventArgs(sessionCreator.CreateSession(terminal)));
                }
                catch { }
            }
        }
    }
}
