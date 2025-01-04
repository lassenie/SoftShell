using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Console
{
    /// <summary>
    /// Instantiates a single <see cref="ConsoleTerminalInterface"/>.
    /// </summary>
    public sealed class ConsoleTerminalListener : ITerminalListener
    {
        private static ConsoleTerminalListener _instance = null;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <inheritdoc/>
        public event TerminalConnectedHandler TerminalConnected;

        /// <summary>
        /// The one and only instance of the <see cref="ConsoleTerminalListener"/>.
        /// </summary>
        public static ConsoleTerminalListener Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ConsoleTerminalListener();

                return _instance;
            }
        }

        /// <summary>
        /// Private constructor.
        /// </summary>
        private ConsoleTerminalListener() { }

        /// <inheritdoc/>
        public async Task RunAsync(ISessionCreator sessionCreator)
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var session = sessionCreator.CreateSession(new ConsoleTerminalInterface());
                TerminalConnected?.Invoke(this, new TerminalConnectedEventArgs(session));

                while (!_cancellationTokenSource.IsCancellationRequested && !session.IsEnded)
                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);

                System.Console.WriteLine();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
