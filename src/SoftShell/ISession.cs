using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// Interface of a SoftShell user session object.
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// Unique ID of the session.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// The host of the session.
        /// </summary>
        ISessionHost Host { get; }

        /// <summary>
        /// Is the session ended?
        /// </summary>
        bool IsEnded { get; }

        /// <summary>
        /// Current state of the session.
        /// </summary>
        SessionState State { get; }

        /// <summary>
        /// Window width as number of characters per line of the terminal running the session. Null if unknown.
        /// </summary>
        int? TerminalWindowWidth { get; }

        /// <summary>
        /// Window height as number of lines of the terminal running the session. Null if unknown.
        /// </summary>
        int? TerminalWindowHeight { get; }

        /// <summary>
        /// A string, e.g. a TCP/IP endpoint, representing the instance of the terminal running the session.
        /// </summary>
        string TerminalInstanceInfo { get; }

        /// <summary>
        /// A string representing the type of terminal running the session.
        /// </summary>
        string TerminalType { get; }

        /// <summary>
        /// Request the session to end when finished running current commands.
        /// </summary>
        void RequestEnd();
    }

    /// <summary>
    /// State of a session.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// The session has not started yet.
        /// </summary>
        NotStarted = default,

        /// <summary>
        /// The user is logging in.
        /// </summary>
        Login,

        /// <summary>
        /// The session is active.
        /// </summary>
        Running,

        /// <summary>
        /// The session is about to end.
        /// </summary>
        Ending,

        /// <summary>
        /// The session is ended.
        /// </summary>
        Ended
    }
}
