using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command for terminating the SoftShell session. See command help info for further details.
    /// </summary>
    public class ExitCommand : StdCommand
    {
        /// <inheritdoc/>
        protected override string Name => "exit";

        /// <inheritdoc/>
        public override string Description => "Terminates the SoftShell session (not the application).";

        /// <inheritdoc/>
        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            return context.TerminateSessionAsync(hard: false);
        }
    }
}
