using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.IO
{
    /// <summary>
    /// Interface for a command to write to standard output.
    /// </summary>
    public interface ICommandOutput
    {
        /// <summary>
        /// Is the output piped to another command's input?
        /// </summary>
        bool IsPiped { get; }

        /// <summary>
        /// Terminal window width as number of characters per line. Null if unknown.
        /// </summary>
        int? WindowWidth { get; }

        /// <summary>
        /// Terminal window height as number of lines. Null if unknown.
        /// </summary>
        int? WindowHeight { get; }

        /// <summary>
        /// Line termination used by the terminal, i.e. "\n" or "\r\n".
        /// </summary>
        string LineTermination { get; }

        /// <summary>
        /// Asynchronously writes text to standard output.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <returns>A running task that performs the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the command is cancelled.</exception>
        Task WriteAsync(string text);

        /// <summary>
        /// Asynchronously writes a line termination to standard output.
        /// </summary>
        /// <returns>A running task that performs the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the command is cancelled.</exception>
        Task WriteLineAsync();

        /// <summary>
        /// Asynchronously writes text, followed by a line termination, to standard output.
        /// </summary>
        /// <returns>A running task that performs the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the command is cancelled.</exception>
        Task WriteLineAsync(string text);

        /// <summary>
        /// Asynchronously clears the screen on standard output, if possible.
        /// </summary>
        /// <returns>A running task that performs the operation.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the command is cancelled.</exception>
        Task ClearScreenAsync();

        /// <summary>
        /// Automatically called when a command ends.
        /// </summary>
        /// <returns>A running task that performs the operation.</returns>
        Task CommandOutputEndAsync();
    }
}
