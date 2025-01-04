using System;
using System.Collections.Generic;
using System.Reflection;

namespace SoftShell
{
    /// <summary>
    /// Host interface for SoftShell sessions. Implemented by the <see cref="SoftShellHost"/> class.
    /// </summary>
    public interface ISessionHost
    {
        /// <summary>
        /// User authorization.
        /// </summary>
        IUserAuthentication UserAuthentication { get; }

        /// <summary>
        /// Possible commands available.
        /// </summary>
        IEnumerable<Command> Commands { get; }

        /// <summary>
        /// Gets text lines for to be written on the terminal when a session is started.
        /// </summary>
        /// <returns>Text lines to write on the terminal. Null or empty collection if none.</returns>
        IEnumerable<string> GetSessionStartInfo();

        /// <summary>
        /// Terminates a session and the connected terminal for the session.
        /// Blocking call that returns when the session is ended.
        /// If the session is already ended, the method just returns.
        /// </summary>
        /// <param name="sessionId">ID of the session to end. If unknown, the method just returns.</param>
        /// <param name="hard">Use hard termination, i.e. immediately terminate the session before the command ends?</param>
        void TerminateSession(int sessionId, bool hard);
    }
}
