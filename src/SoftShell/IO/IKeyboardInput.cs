using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.IO
{
    /// <summary>
    /// Interface for a command to explicitly read keyboard input (not standard input).
    /// </summary>
    public interface IKeyboardInput
    {
        /// <summary>
        /// Is keyboard input available to the command? This should be checked once before a command starts reading keyboard input.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Is keyboard input ended for the command? This is the case if command execution is cancelled.
        /// </summary>
        bool IsEnded { get; }

        /// <summary>
        /// Asynchronously flushes keyboard input.
        /// </summary>
        /// <returns>A running task that performs the operation.</returns>
        Task FlushInputAsync();

        /// <summary>
        /// Asynchronously reads input from the keyboard, if any. Finishes immediately.
        /// </summary>
        /// <param name="echo">Should keyboard input be echoed on the terminal?</param>
        /// <returns>
        /// A running task that executes the operation and provides the resulting keyboard input
        /// (pairs of <see cref="KeyAction"/> and a character ('\0' if none), or an empty collection if no keyboard input is currently provided).
        /// </returns>
        Task<IEnumerable<(KeyAction action, char character)>> TryReadAsync(bool echo = true);

        /// <summary>
        /// Asynchronously reads keyboard input. Finishes when input is available, or if the command is cancelled.
        /// </summary>
        /// <param name="echo">Should the keyboard input be echoed on the terminal?</param>
        /// <returns>
        /// A running task that executes the operation and provides the resulting keyboard input
        /// (pairs of <see cref="KeyAction"/> and a character ('\0' if none)).
        /// </returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task<IEnumerable<(KeyAction action, char character)>> ReadAsync(bool echo = true);

        /// <summary>
        /// Asynchronously reads a line of text from the user. Finished when a text line is terminated
        /// or if the command is cancelled.
        /// </summary>
        /// <param name="echo">Should the user's input be echoed on the terminal?</param>
        /// <returns>
        /// A running task that executes the operation and provides the resulting keyboard input
        /// as an entered string.
        /// </returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled.</exception>
        Task<string> ReadLineAsync(bool echo = true);
    }
}
