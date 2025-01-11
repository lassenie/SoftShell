using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command for clearing the terminal's screen. See command help info for further details.
    /// </summary>
    public class ClearScreenCommand : StdCommand
    {
        /// <inheritdoc/>
        protected override string Name => "cls";

        /// <inheritdoc/>
        public override string Description => "Clears the terminal's screen.";

        /// <inheritdoc/>
        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            return context.Output.ClearScreenAsync();
        }
    }
}
