using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell
{
    /// <summary>
    /// Common interface to a terminal where the user gives input and receives output.
    /// </summary>
    public interface ITerminalInterface : IDisposable
    {
        /// <summary>
        /// A string representing the type of terminal implementation.
        /// </summary>
        string TerminalType { get; }

        /// <summary>
        /// A string representing the terminal instance, e.g. a TCP/IP endpoint.
        /// </summary>
        string TerminalInstanceInfo { get; }

        /// <summary>
        /// Line termination used by the terminal, i.e. "\n" or "\r\n".
        /// </summary>
        string LineTermination { get; }

        /// <summary>
        /// Character encoding used by the terminal.
        /// </summary>
        Encoding Encoding { get; }

        /// <summary>
        /// Window width as number of characters per line. Null if unknown.
        /// </summary>
        int? WindowWidth { get; }

        /// <summary>
        /// Window height as number of lines. Null if unknown.
        /// </summary>
        int? WindowHeight { get; }

        /// <summary>
        /// Gets the current text color. Null if default.
        /// </summary>
        ConsoleColor? CurrentTextColor { get; }

        /// <summary>
        /// Event that is fired when the user requests to cancel a task/command (Ctrl-C).
        /// </summary>
        event EventHandler TaskCancelRequest;

        /// <summary>
        /// Asynchronously flushes buffered input from the terminal.
        /// </summary>
        /// <param name="cancelToken">Cancellation token that is signalled if the operation should be cancelled.</param>
        /// <returns>A running task that executes the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task FlushInputAsync(CancellationToken cancelToken);

        /// <summary>
        /// Asynchronously reads input from the user. Finishes when input is available, or if the operation is cancelled.
        /// </summary>
        /// <param name="cancelToken">Cancellation token what is signalled if the operation should be cancelled.</param>
        /// <param name="echo">Should the user's input be echoed on the terminal?</param>
        /// <returns>
        /// A running task that executes the operation and provides the resulting user input
        /// (pairs of <see cref="KeyAction"/> and a character ('\0' if none)).
        /// </returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task<IEnumerable<(KeyAction action, char character)>> ReadAsync(CancellationToken cancelToken, bool echo);

        /// <summary>
        /// Asynchronously reads input from the user, if any. Finishes immediately.
        /// </summary>
        /// <param name="echo">Should the user's input be echoed on the terminal?</param>
        /// <returns>
        /// A running task that executes the operation and provides the resulting user input
        /// (pairs of <see cref="KeyAction"/> and a character ('\0' if none), or an empty collection if no user input is currently provided).
        /// </returns>
        Task<IEnumerable<(KeyAction action, char character)>> TryReadAsync(bool echo);

        /// <summary>
        /// Asynchronously reads a line of text from the user. Finished when a text line is terminated,
        /// if an escaping action is performed (clears the text), or if the operation is cancelled.
        /// </summary>
        /// <param name="cancelToken">Cancellation token what is signalled if the operation should be cancelled.</param>
        /// <param name="echo">Should the user's input be echoed on the terminal?</param>
        /// <param name="initialString">An initial text string to start with.</param>
        /// <param name="isEscapingCheck">
        /// An optional delegate for checking if the user has performed an escaping action.
        /// The delegate tages a <see cref="KeyAction"/> and a character entered (or '\0' if no character).
        /// It then returns true if considered an escaping action. 
        /// </param>
        /// <returns>
        /// A running task that executes the operation and provides the resulting user input:
        /// - The entered string (empty if none)
        /// - A <see cref="KeyAction"/> (<see cref="KeyAction.None"/> if OK, or an escaping action)
        /// - An escaping character ('\0' if none).
        /// </returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task<(string strOut, KeyAction escapingAction, char escapingChar)> ReadLineAsync(CancellationToken cancelToken, bool echo, string initialString, Func<KeyAction, char, bool> isEscapingCheck);

        /// <summary>
        /// Asynchronously writes a text to the terminal.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="cancelToken">Cancellation token what is signalled if the operation should be cancelled.</param>
        /// <returns>A running task that executes the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task WriteAsync(string text, CancellationToken cancelToken);

        /// <summary>
        /// Asynchronously writes a terminated line of text to the terminal.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="cancelToken">Cancellation token what is signalled if the operation should be cancelled.</param>
        /// <returns>A running task that executes the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task WriteLineAsync(string text, CancellationToken cancelToken);

        /// <summary>
        /// Asynchronously clears the screen on the terminal.
        /// </summary>
        /// <param name="cancelToken">Cancellation token what is signalled if the operation should be cancelled.</param>
        /// <returns>A running task that executes the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task ClearScreenAsync(CancellationToken cancelToken);

        /// <summary>
        /// Asynchronously sets the text color on the terminal. The text color will be used for subsequent writing.
        /// </summary>
        /// <param name="color">Color to set. If null, the default color for the terminal is set.</param>
        /// <param name="cancelToken">Cancellation token what is signalled if the operation should be cancelled.</param>
        /// <returns>A running task that executes the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task SetTextColorAsync(ConsoleColor? color, CancellationToken cancelToken);

        /// <summary>
        /// This method is called in the beginning of a command chain (pipe) execution.
        /// Mainly used for testing - terminal interfaces will normally not implement any functionality in this method.
        /// </summary>
        /// <returns>A running task that executes the operation.</returns>
        Task ReportCommandChainBeginningOfOutputAsync();

        /// <summary>
        /// This method is called in the end of a command chain (pipe) execution.
        /// Mainly used for testing - terminal interfaces will normally not implement any functionality in this method.
        /// </summary>
        /// <returns>A running task that executes the operation.</returns>
        Task ReportCommandChainEndOfOutputAsync();
    }

    /// <summary>
    /// Key actions from a terminal.
    /// </summary>
    public enum KeyAction
    {
        /// <summary>
        /// No action - do nothing.
        /// </summary>
        None = 0,

        /// <summary>
        /// A character is received.
        /// </summary>
        Character,

        /// <summary>
        /// The user pressed arrow forward key.
        /// </summary>
        ArrowForward,

        /// <summary>
        /// The user pressed arrow back key.
        /// </summary>
        ArrowBack,

        /// <summary>
        /// The user pressed arrow up key.
        /// </summary>
        ArrowUp,

        /// <summary>
        /// The user pressed arrow down key.
        /// </summary>
        ArrowDown,

        /// <summary>
        /// The user pressed the Home key.
        /// </summary>
        Home,

        /// <summary>
        /// The user pressed the End key.
        /// </summary>
        End
    }
}
