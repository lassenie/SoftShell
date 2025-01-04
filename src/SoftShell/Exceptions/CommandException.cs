using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell.Exceptions
{
    /// <summary>
    /// An exception that carries information about a command that has failed.
    /// </summary>
    internal class CommandException : Exception
    {
        /// <summary>
        /// Command that has failed.
        /// </summary>
        public Command Command { get; }

        /// <summary>
        /// Command line of the command that has failed.
        /// </summary>
        public string CommandLine { get; }

        /// <summary>
        /// Constructor that takes an inner exception.
        /// </summary>
        public CommandException(Exception innerException, Command command, string commandLine)
            : base(innerException.Message, innerException)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            CommandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));
        }

        /// <summary>
        /// Constructor not taking an inner exception.
        /// </summary>
        protected CommandException(Command command, string commandLine)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            CommandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));
        }
    }
}
