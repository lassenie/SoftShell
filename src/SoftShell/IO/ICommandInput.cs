using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.IO
{
    /// <summary>
    /// Interface for a command to read standard input.
    /// </summary>
    public interface ICommandInput
    {
        /// <summary>
        /// Is the input piped from another command's output?
        /// </summary>
        bool IsPiped { get; }

        /// <summary>
        /// Is the input ended for the command? This is the case if command execution is cancelled, or if having a redirected null input.
        /// </summary>
        bool IsEnded { get; }

        /// <summary>
        /// Asynchronously flushes the standard input.
        /// </summary>
        /// <returns>A running task that performs the operation.</returns>
        Task FlushInputAsync();

        /// <summary>
        /// Asynchronously reads characters, if any, from standard input. Finishes immediately.
        /// </summary>
        /// <returns>
        /// A running task that executes the operation and provides the resulting input characters in a string
        /// (empty if no input characters are currently available).
        /// </returns>
        Task<string> TryReadAsync();

        /// <summary>
        /// Asynchronously reads characters, from standard input.
        /// Finishes when characters are available, or if the command is cancelled.
        /// </summary>
        /// <returns>
        /// A running task that executes the operation and provides the resulting input characters.
        /// </returns>
        /// <exception cref="TaskCanceledException">Thrown if the command is cancelled.</exception>
        Task<string> ReadAsync();

        /// <summary>
        /// Asynchronously reads a line of text from standard input. Finished when a text line is terminated
        /// or if the command is cancelled.
        /// </summary>
        /// <returns>
        /// A running task that executes the operation and provides the resulting input as a string.
        /// </returns>
        /// <exception cref="TaskCanceledException">Thrown if the command is cancelled.</exception>
        Task<string> ReadLineAsync();
    }
}
