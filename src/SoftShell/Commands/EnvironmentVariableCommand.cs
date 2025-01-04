using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Commands
{
    public class EnvironmentVariableCommand : StdCommand
    {
        public override string Name => "Env";

        public override string Description => "Handling of environment variables for the application's process.";

        public EnvironmentVariableCommand()
        {
            HasNonSubcommand("Lists environment variables for the application's process.");

            HasSubcommand("get", "Gets the value of an environment variable, if existing, for the application's process.")
                .HasRequiredParameter("variable-name", "The name of the environment variable.", val => val);

            HasSubcommand("set", "Creates or modifies an environment variable for the application's process.")
                .HasRequiredParameter("variable-name", "The name of the environment variable.", val => val)
                .HasRequiredParameter("value", "The value to assign to the variable.", val => val);

            HasSubcommand("delete", "Deletes an environment variable, if existing, for the application's process.")
                .HasRequiredParameter("variable-name", "The name of the environment variable.", val => val);
        }

        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (!subcommand.IsSubcommand)
                return ShowVariableListAsync(context);
            else
            {
                switch (subcommand.Name)
                {
                    case "get":
                        var value = Environment.GetEnvironmentVariable(args.Get(0).ToString(), EnvironmentVariableTarget.Process);

                        if (value != null)
                            return context.Output.WriteLineAsync(value);
                        else
                            return Task.CompletedTask;

                    case "set":
                        Environment.SetEnvironmentVariable(args.Get(0).ToString(), args.Get(1).ToString(), EnvironmentVariableTarget.Process);
                        return Task.CompletedTask;

                    case "delete":
                        Environment.SetEnvironmentVariable(args.Get(0).ToString(), null, EnvironmentVariableTarget.Process);
                        return Task.CompletedTask;

                    default:
                        throw new Exception($"Unhandled subcommand {subcommand.Name}");
                }
            }
        }

        private async Task ShowVariableListAsync(IStdCommandExecutionContext context)
        {
            var variables = new List<(string key, string value)>();
            
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
            {
                variables.Add((key: variable.Key?.ToString() ?? string.Empty,
                               value: variable.Value?.ToString() ?? string.Empty));
            }

            var lines = TextFormatting.GetAlignedColumnStrings(variables.OrderBy(v => v.key),
                                                               " ",
                                                               ("Name", v => v.key, TextAlignment.Start),
                                                               ("Value", v => v.value, TextAlignment.Start));

            foreach (var line in lines)
                await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
