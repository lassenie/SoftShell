using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FxSsh;
using FxSsh.Services;

namespace SoftShell.Ssh
{
    /// <summary>
    /// Listens for incoming SSH connections and instantiates an <see cref="SshTerminalInterface"/> for each.
    /// </summary>
    public class SshTerminalListener : ITerminalListener
    {
        // Creates SoftShell sessions for connecting terminals; set when the listener is run.
        ISessionCreator? _sessionCreator = null;

        // The underlying SSH server. Created in the constructor so host keys can be added before running.
        readonly SshServer _server;

        /// <summary>
        /// IP end point for incoming SSH connections.
        /// </summary>
        public IPEndPoint IPEndPoint { get; private set; }

        /// <summary>Sets the ECDSA host key (PEM format) for the nistp256 curve.</summary>
        public string? KeyEcdsaNistp256 { set => _server.AddHostKey("ecdsa-sha2-nistp256", value); }

        /// <summary>Sets the ECDSA host key (PEM format) for the nistp384 curve.</summary>
        public string? KeyEcdsaNistp384 { set => _server.AddHostKey("ecdsa-sha2-nistp384", value); }

        /// <summary>Sets the ECDSA host key (PEM format) for the nistp521 curve.</summary>
        public string? KeyEcdsaNistp521 { set => _server.AddHostKey("ecdsa-sha2-nistp521", value); }

        /// <inheritdoc/>
        public event TerminalConnectedHandler? TerminalConnected;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">IP address for incoming SSH connections.</param>
        /// <param name="port">TCP port for incoming SSH connections.</param>
        /// <param name="hostKeyDirectory">Directory in which the RSA host key file is stored. The key is loaded from here if present, otherwise generated and saved.</param>
        public SshTerminalListener(long address, int port, string hostKeyDirectory) : this(new IPAddress(address), port, hostKeyDirectory) { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ipAddress">IP address for incoming SSH connections.</param>
        /// <param name="port">TCP port for incoming SSH connections.</param>
        /// <param name="hostKeyDirectory">Directory in which the RSA host key file is stored. The key is loaded from here if present, otherwise generated and saved.</param>
        public SshTerminalListener(IPAddress ipAddress, int port, string hostKeyDirectory)
        {
            if (hostKeyDirectory is null)
                throw new ArgumentNullException(nameof(hostKeyDirectory));

            IPEndPoint = new IPEndPoint(ipAddress, port);

            // The server is created here (not in RunAsync) so that host keys added below or by the
            // caller before the listener is started are kept on this very server instance.
            // The banner must be a valid SSH identification string starting with "SSH-2.0-".
            _server = new SshServer(new StartingInfo(IPEndPoint.Address, IPEndPoint.Port, "SSH-2.0-SoftShell"));

            // Load or create the RSA host key. Persisting it keeps the server's identity stable
            // across restarts, so connecting clients don't warn that the host key changed.
            var rsaKeyPem = LoadOrCreateRsaHostKey(hostKeyDirectory);
            _server.AddHostKey("rsa-sha2-256", rsaKeyPem);
            _server.AddHostKey("rsa-sha2-512", rsaKeyPem);
        }

        /// <summary>
        /// Loads the RSA host key (PEM format) from <paramref name="directory"/>, generating
        /// and persisting a new one on first use.
        /// </summary>
        /// <param name="directory">Directory holding the RSA host key file.</param>
        /// <returns>The RSA host key in PEM format.</returns>
        private static string LoadOrCreateRsaHostKey(string directory)
        {
            var keyFilePath = Path.Combine(directory, "ssh_host_rsa.pem");

            if (File.Exists(keyFilePath))
                return File.ReadAllText(keyFilePath);

            Directory.CreateDirectory(directory);

            var pem = KeyGenerator.GenerateRsaKeyPem(2048);
            File.WriteAllText(keyFilePath, pem);

            return pem;
        }

        /// <inheritdoc/>
        public Task RunAsync(ISessionCreator? sessionCreator)
        {
            _sessionCreator = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
            _server.ConnectionAccepted += Server_ConnectionAccepted;
            _server.Start();

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _server.Dispose();
        }

        /// <summary>
        /// Handles an accepted SSH connection by creating a terminal interface and a
        /// SoftShell session for it, then raising <see cref="TerminalConnected"/>.
        /// </summary>
        private void Server_ConnectionAccepted(object? sender, Session e)
        {
            var terminal = new SshTerminalInterface(e);

            TerminalConnected?.Invoke(this, new TerminalConnectedEventArgs(_sessionCreator!.CreateSession(terminal)));
        }
    }
}
