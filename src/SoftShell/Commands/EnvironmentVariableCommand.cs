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
        /// <summary>
        /// Host interface for accessing the process's environment variables. Can be mocked for unit testing.
        /// </summary>
        public interface IHost
        {
            /// <summary>
            /// Gets all environment variables for the application's process as name/value pairs.
            /// Wraps <see cref="Environment.GetEnvironmentVariables(EnvironmentVariableTarget)"/> in the default implementation.
            /// </summary>
            IEnumerable<(string name, string value)> GetEnvironmentVariables();

            /// <summary>
            /// Gets the value of a single environment variable, or null if not existing.
            /// Wraps <see cref="Environment.GetEnvironmentVariable(string, EnvironmentVariableTarget)"/> in the default implementation.
            /// </summary>
            string GetEnvironmentVariable(string name);

            /// <summary>
            /// Creates, modifies or (when value is null) deletes an environment variable.
            /// Wraps <see cref="Environment.SetEnvironmentVariable(string, string, EnvironmentVariableTarget)"/> in the default implementation.
            /// </summary>
            void SetEnvironmentVariable(string name, string value);
        }

        private IHost _host;

        protected override string Name => "env";

        public override string Description => "Handling of environment variables for the application's process.";

        /// <summary>
        /// Constructor that creates the command object using a default <see cref="IHost"/> implementation.
        /// </summary>
        internal EnvironmentVariableCommand() : this(new DefaultHost()) { }

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="IHost"/> implementation.
        /// </summary>
        internal EnvironmentVariableCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

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
                switch (subcommand.Name.ToLowerInvariant())
                {
                    case "get":
                        var value = _host.GetEnvironmentVariable(args.Get(0).ToString());

                        if (value != null)
                            return context.Output.WriteLineAsync(value);
                        else
                            return Task.CompletedTask;

                    case "set":
                        _host.SetEnvironmentVariable(args.Get(0).ToString(), args.Get(1).ToString());
                        return Task.CompletedTask;

                    case "delete":
                        _host.SetEnvironmentVariable(args.Get(0).ToString(), null);
                        return Task.CompletedTask;

                    default:
                        throw new Exception($"Unhandled subcommand {subcommand.Name}");
                }
            }
        }

        private async Task ShowVariableListAsync(IStdCommandExecutionContext context)
        {
            var variables = _host.GetEnvironmentVariables()
                                 .Select(v => (key: v.name ?? string.Empty, value: v.value ?? string.Empty))
                                 .ToList();

            var lines = TextFormatting.GetAlignedColumnStrings(variables.OrderBy(v => v.key),
                                                               " ",
                                                               ("Name", v => v.key, TextAlignment.Start),
                                                               ("Value", v => v.value, TextAlignment.Start));

            foreach (var line in lines)
                await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }

        private class DefaultHost : IHost
        {
            public IEnumerable<(string name, string value)> GetEnvironmentVariables()
            {
                foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
                {
                    yield return (name: variable.Key?.ToString() ?? string.Empty,
                                  value: variable.Value?.ToString() ?? string.Empty);
                }
            }

            public string GetEnvironmentVariable(string name)
                => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

            public void SetEnvironmentVariable(string name, string value)
                => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }
    }
}
