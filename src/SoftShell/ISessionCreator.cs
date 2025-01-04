using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// Interface for creation of SoftShell user sessions.
    /// </summary>
    public interface ISessionCreator
    {
        /// <summary>
        /// Creates a new user session.
        /// </summary>
        /// <param name="terminal">The terminal that initiates the new session.</param>
        /// <returns>Created session object.</returns>
        ISession CreateSession(ITerminalInterface terminal);
    }
}
