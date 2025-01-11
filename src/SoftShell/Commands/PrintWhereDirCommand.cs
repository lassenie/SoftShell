using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.Commands
{
    internal class PrintWhereDirCommand : StdCommand
    {
        protected override string Name => "pwd";

        public override string Description => "Prints the current working directory of the application.";

        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            return context.Output.WriteLineAsync(Directory.GetCurrentDirectory());
        }
    }
}
