using SoftShell;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDemo1.Commands
{
    public class AppInfoCommand : StdCommand
    {
        public override string Name => "Info";

        public override string Description => "App-specific info command.";

        public AppInfoCommand()
        {
            HasRequiredParameter("x", "Parameter X.", val => Convert.ToInt32(val));
            HasOptionalParameter("y", "Parameter Y.", val => val);
            HasFlagOption("a", "Option A.");
            HasRequiredValueOption("b", "Option B.", val => new string(val.Reverse().ToArray()));
        }

        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            await context.Output.WriteLineAsync("App-specific info.").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Arguments: {string.Join(" ", args.Select(arg => arg.value))}").ConfigureAwait(false);

            if (options.Any())
            {
                await context.Output.WriteLineAsync("Options:").ConfigureAwait(false);

                foreach (var option in options)
                {
                    if (string.IsNullOrEmpty(option.value?.ToString()))
                        await context.Output.WriteLineAsync($"- {option.name}").ConfigureAwait(false);
                    else
                        await context.Output.WriteLineAsync($"- {option.name}={option.value}").ConfigureAwait(false);
                }
            }
        }
    }
}
