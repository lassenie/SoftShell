using SoftShell.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command for show help information for registered commands.
    /// </summary>
    public class HelpCommand : StdCommand
    {
        /// <summary>
        /// Interface for providing the registered commands to show help for.
        /// </summary>
        internal interface ICommandCollectionProvider
        {
            /// <summary>
            /// Gets registered commands.
            /// </summary>
            /// <returns></returns>
            IEnumerable<Command> GetCommands();
        }

        private ICommandCollectionProvider _commandCollectionProvider;

        /// <inheritdoc/>
        protected override string Name => "help";

        /// <inheritdoc/>
        public override string Description => "Shows help information, either as a list of commands or detailed command help info.";

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="ICommandCollectionProvider"/> implementation.
        /// </summary>
        internal HelpCommand(ICommandCollectionProvider provider)
        {
            _commandCollectionProvider = provider ?? throw new ArgumentNullException(nameof(provider));

            HasOptionalParameter("command", "Command to get detailed help for.", val => val);
            HasOptionalParameter("subcommand", "Subcommand to get detailed help for.", val => val);
            HasFlagOption("groups", "Lists command groups only (not allowed together with the command and subcommand parameters).");
            HasValueOption("group", "Lists only commands for a given group (not allowed together with the command and subcommand parameters).", val => val);
        }

        /// <inheritdoc/>
        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            var commands = _commandCollectionProvider.GetCommands() ?? throw new Exception("Failed to get information about commands.");

            if (options.HasFlag("groups") && args.Any())
                throw new Exception("The /groups option is not allowed together with the command argument.");
            if (options.TryGet("group", out var _) && args.Any())
                throw new Exception("The /group option is not allowed together with the command argument.");

            if (options.HasFlag("groups"))
                return ShowGroupsHelpAsync(context, commands.GroupBy(cmd => cmd.Group).Select(grp => grp.Key));

            if (args.TryGetAs<string>(0, out var commandName) && !string.IsNullOrWhiteSpace(commandName))
            {
                if (args.TryGetAs<string>(1, out var subcommandName) && !string.IsNullOrEmpty(subcommandName))
                    return ShowSubcommandHelpAsync(context, commandName, subcommandName, commands);
                else
                    return ShowCommandHelpAsync(context, commandName, commands);
            }
            else
                return ShowGeneralHelpAsync(context, commands);
        }

        private async Task ShowGroupsHelpAsync(IStdCommandExecutionContext context, IEnumerable<CommandGroup> groups)
        {
            var lines = TextFormatting.GetAlignedColumnStrings(groups.OrderBy(grp => grp.IsCore ? 1 : 0).ThenBy(grp => grp.Name).ToList(),
                                                               "  ",
                                                               ("Name",   grp => grp.Name,   TextAlignment.Start),
                                                               ("Prefix", grp => grp.Prefix, TextAlignment.Start));

            foreach (var line in lines)
                await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }

        private Task ShowCommandHelpAsync(IStdCommandExecutionContext context, string commandName, IEnumerable<Command> commands)
        {
            var candidates = commands.Where(cmd => cmd.CommandNames.Any(syn => syn.Equals(commandName, StringComparison.InvariantCultureIgnoreCase))).ToList();

            if (candidates.Count == 1)
            {
                var helpText = candidates[0].GetHelpText(context, null);

                if (!helpText.EndsWith("\n"))
                    helpText = helpText + context.Output.LineTermination;

                return context.Output.WriteAsync(helpText); // Final line termination is included in the help text
            }
            else
            {
                if (candidates.Count > 0)
                {
                    throw new Exception(GetAmbiguousCommandExceptionText(commandName, candidates));
                }
                else
                {
                    return context.Output.WriteLineAsync($"Unknown command: {commandName}");
                }
            }
        }

        private Task ShowSubcommandHelpAsync(IStdCommandExecutionContext context, string commandName, string subcommandName, IEnumerable<Command> commands)
        {
            var candidates = commands.Where(cmd => cmd.CommandNames.Any(syn => syn.Equals(commandName, StringComparison.InvariantCultureIgnoreCase))).ToList();

            if (candidates.Count == 1)
            {
                var helpText = candidates[0].GetHelpText(context, subcommandName);

                if (!helpText.EndsWith("\n"))
                    helpText = helpText + context.Output.LineTermination;

                return context.Output.WriteAsync(helpText); // Final line termination is included in the help text
            }
            else
            {
                if (candidates.Count > 0)
                {
                    throw new Exception(GetAmbiguousCommandExceptionText(commandName, candidates));
                }
                else
                {
                    throw new Exception($"Unknown command: {commandName}");
                }
            }
        }

        private async Task ShowGeneralHelpAsync(IStdCommandExecutionContext context, IEnumerable<Command> commands)
        {
            bool isFirst = true;

            context.Options.TryGetAs<string>("group", out var groupStr);

            foreach (var group in commands.GroupBy(cmd => cmd.Group).OrderBy(grp => grp.Key.IsCore ? 1 : 0).ThenBy(grp => grp.Key.Prefix.ToLowerInvariant()))
            {
                // Not the wanted group?
                if (!string.IsNullOrEmpty(groupStr) &&
                    !groupStr.Equals(group.Key.Name, StringComparison.InvariantCultureIgnoreCase) &&
                    !groupStr.Equals(group.Key.Prefix, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (!isFirst)
                    await context.Output.WriteLineAsync().ConfigureAwait(false);

                if (group.Key.IsCore)
                    await context.Output.WriteLineAsync($"{group.Key.Name} commands:").ConfigureAwait(false);
                else
                    await context.Output.WriteLineAsync($"{group.Key.Name} ({group.Key.Prefix}) commands:").ConfigureAwait(false);

                var lines = TextFormatting.GetAlignedColumnStrings(group.OrderBy(cmd => cmd.CommandName).ToList(),
                                                                   "  ",
                                                                   ("", cmd => cmd.CommandName, TextAlignment.Start),
                                                                   ("", cmd => cmd.Description, TextAlignment.Start));

                foreach (var line in lines)
                    await context.Output.WriteLineAsync(line).ConfigureAwait(false);

                isFirst = false;
            }

            await context.Output.WriteLineAsync().ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Type '{this.Name} <command-name>' to get detailed help for a command.").ConfigureAwait(false);
        }
    }
}
