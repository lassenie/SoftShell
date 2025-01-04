using SoftShell.Exceptions;
using SoftShell.Execution;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.IO
{
    /// <summary>
    /// Interface for providing standard error output from a command.
    /// This interface is not intended for command implementations to use directly.
    /// </summary>
    internal interface ICommandExceptionOutput
    {
        /// <summary>
        /// Automatically called if the command execution throws an unhandled exception.
        /// The exception will ripple through the remaining command pipe, if any, cancelling the execution.
        /// Exception information will be written to the terminal.
        /// </summary>
        /// <param name="exception">The unhandled exception.</param>
        /// <returns>A running task that performs the operation.</returns>
        Task HandleExceptionAsync(CommandException exception);
    }
}
