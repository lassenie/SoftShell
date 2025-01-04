using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// Context interface for execution of <see cref="StdCommand"/>-based commands.
    /// Injected in execution methods in <see cref="StdCommand"/>-derived classes for accessing input/output etc.
    /// </summary>
    public interface IStdCommandExecutionContext : ICommandExecutionContext
    {
        /// <summary>
        /// Subcommand (or non-subcommand) to execute.
        /// </summary>
        Subcommand Subcommand { get; }

        /// <summary>
        /// Parsed arguments.
        /// </summary>
        CommandArgs Args { get; }

        /// <summary>
        /// Parsed options (values and/or flags).
        /// </summary>
        CommandOptions Options { get; }

    }
}
