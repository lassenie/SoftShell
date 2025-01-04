using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell.Exceptions
{
    /// <summary>
    /// Exception thrown internally when a command is cancelled.
    /// </summary>
    internal class CommandCancelledException : CommandException
    {
        /// <inheritdoc/>
        public override string Message => "Command cancelled.";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command that was cancelled.</param>
        /// <param name="commandLine">The command line of the command that was cancelled.</param>
        public CommandCancelledException(Command command, string commandLine) : base(command, commandLine)
        {
        }
    }
}
