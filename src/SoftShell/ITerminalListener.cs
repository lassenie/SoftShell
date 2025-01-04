using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell
{
    /// <summary>
    /// Common interface for creating interfaces for connecting terminals.
    /// </summary>
    public interface ITerminalListener : IDisposable
    {
        /// <summary>
        /// Event that is fired when a terminal is connected.
        /// </summary>
        event TerminalConnectedHandler TerminalConnected;

        /// <summary>
        /// Runs the terminal listener.
        /// </summary>
        /// <param name="sessionCreator">Interface for creation of SoftShell user sessions when terminals connect.</param>
        /// <returns>A running task that executes the operation.</returns>
        Task RunAsync(ISessionCreator sessionCreator);
    }

    /// <summary>
    /// Delegate for <see cref="ITerminalListener.TerminalConnected"/> event handlers.
    /// </summary>
    /// <param name="sender">The object firing the event.</param>
    /// <param name="e">Event arguments.</param>
    public delegate void TerminalConnectedHandler(object sender, TerminalConnectedEventArgs e);

    /// <summary>
    /// Arguments for <see cref="ITerminalListener.TerminalConnected"/> events.
    /// </summary>
    public class TerminalConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// The session initiated for the connected terminal.
        /// </summary>
        public ISession Session { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The session initiated for the connected terminal.</param>
        public TerminalConnectedEventArgs(ISession session)
        {
            Session = session;
        }
    }
}
