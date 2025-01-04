using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDemo1.Commands
{
    public class AppCommand2 : StdCommand
    {
        public override string Name => "App2";

        public override string Description => "App-specific command 2";

        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            await context.Output.WriteLineAsync("This is the custom app-specific command 2.").ConfigureAwait(false);

            await context.Output.WriteAsync("Awaiting text input: ").ConfigureAwait(false);
            await context.Input.ReadLineAsync().ConfigureAwait(false);
        }
    }
}
