using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDemo1.Commands
{
    public class AppExitCommand : StdCommand
    {
        public override string Name => "AppExit";

        public override string Description => "Terminates the entire host application.";

        public Action<int> TerminateProgram { get; }

        public AppExitCommand(Action<int> terminateProgram)
        {
            TerminateProgram = terminateProgram ?? throw new ArgumentNullException(nameof(terminateProgram));

            HasOptionalParameter("exitcode", "Optional exit code for the program (otherwise 0).", val => Convert.ToInt32(val));
        }

        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (!args.TryGetAs<int>("exitcode", out var exitCode))
                exitCode = 0;

            await context.Output.WriteLineAsync($"Terminating the host application with exit code {exitCode}...").ConfigureAwait(false);

            TerminateProgram(exitCode);
        }
    }
}
