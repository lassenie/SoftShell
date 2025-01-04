using SoftShell.Commands.Anonymous;
using SoftShell.Execution;
using SoftShell.Helpers;
using SoftShell.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell
{
    /// <summary>
    /// Base class for commands with standard argument and option parsing.
    /// </summary>
    public abstract class StdCommand : Command
    {
        private Subcommand _nonSubcommand = null;

        private Dictionary<string, Subcommand> _subcommands = new Dictionary<string, Subcommand>();

        /// <summary>
        /// Representation of how to invoke the command without any subcommand, if possible.
        /// </summary>
        public Subcommand NonSubcommand
        {
            get
            {
                // Auto-created if no subcommands.
                // This allows constructing a simple command without arguments, options or subcommands without having to call any Has... methods.
                if (_nonSubcommand == null && !_subcommands.Any())
                    _nonSubcommand = new NonSubcommand(string.Empty);

                return _nonSubcommand;
            }
        }

        /// <summary>
        /// Representations of how to invoke the command with various subcommands, if any.
        /// </summary>
        public ImmutableDictionary<string, Subcommand> Subcommands => ImmutableDictionary<string, Subcommand>.Empty.AddRange(_subcommands);

        /// <inheritdoc/>
        internal override void PostConstruct()
        {
            // Auto-create the non-subcommand if no subcommands
            if (_nonSubcommand == null && !_subcommands.Any())
                _nonSubcommand = new NonSubcommand(string.Empty);
        }

        /// <inheritdoc/>
        /// <param name="context"></param>
        /// <param name="subcommandName"></param>
        /// <returns></returns>
        public override string GetHelpText(ICommandExecutionContext context, string subcommandName)
        {
            if (string.IsNullOrWhiteSpace(subcommandName))
                return GetGeneralHelpText(context);
            else
                return GetSubcommandHelpText(context, subcommandName);
        }

        /// <summary>
        /// Overridable method for executing the command.
        /// </summary>
        /// <param name="context">Extended command execution context.</param>
        /// <param name="subcommand">Subcommand (or non-subcommand) to execute.</param>
        /// <param name="args">Arguments given for the execution.</param>
        /// <param name="options">Option values or flags given for the execution.</param>
        /// <param name="commandLine">
        /// The raw command line given to invoke the command.
        /// If the command is invoked in a pipe, only this command's part of the total command line is provided.
        /// No command line is provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <exception cref="Exception">A command may throw an exception in case of a run-time error.</exception>
        /// <returns>A task for executing the command.</returns>
        protected abstract Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine);

        /// <summary>
        /// Interprets the command arguments in a standard way, supporting subcommands, parameters and options.
        /// Invokes the <see cref="ExecuteAsync(IStdCommandExecutionContext, Subcommand, CommandArgs, CommandOptions)"/>
        /// method which must be overridden for each command class inheriting this <see cref="StdCommand"/> class.
        /// </summary>
        /// <param name="context">Command execution context.</param>
        /// <param name="commandLine">
        /// The raw command line given to invoke the command.
        /// If the command is invoked in a pipe, only this command's part of the total command line is provided.
        /// No command line is provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <param name="tokens">
        /// Parsed tokens from the command line. First token is the command itself.
        /// No tokens are provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <exception cref="Exception">A command may throw an exception in case of a run-time error.</exception>
        /// <returns>A task for executing the command.</returns>
        protected override Task ExecuteAsync(ICommandExecutionContext context, string commandLine, IEnumerable<CommandLineToken> tokens)
        {
            (var args, var options) = CommandLineParser.Parse(tokens);

            var subcommand = GetSubcommand(args);
            var cmdArgs = GetCommandArgs(args, subcommand);
            var cmdOptions = GetCommandOptions(options, subcommand);
            return ExecuteAsync(new StdCommandExecutionContext(context, subcommand, cmdArgs, cmdOptions),
                                subcommand,
                                cmdArgs,
                                cmdOptions,
                                commandLine);
        }

        /// <summary>
        /// Gets the subcommand, if any, that is given for a command execution.
        /// </summary>
        /// <param name="args">Parsed arguments.</param>
        /// <returns>Subcommand (or non-subcommand) to execute.</returns>
        /// <exception cref="Exception">Thrown in case of missing, invalid or unknown subcommand.</exception>
        private Subcommand GetSubcommand(IEnumerable<string> args)
        {
            // Possibly a subcommand provided?
            if (Subcommands.Any() && args.Any())
            {
                var subcommand = Subcommands.FirstOrDefault(subcmd => subcmd.Key.Equals(args.First(), StringComparison.InvariantCultureIgnoreCase));

                // Found a subcommand to return?
                if (subcommand.Value != null)
                    return subcommand.Value;

                if (string.IsNullOrEmpty(args.First()) || args.First().Any(ch => char.IsWhiteSpace(ch)))
                    throw new Exception($"Empty or invalid subcommand '{args.First()}'.");
                else
                    throw new Exception(GetUnknownSubcommandExceptionText(args.First()));
            }

            // Do we need a subcommand but haven't any?
            if ((NonSubcommand is null) && !args.Any())
                throw new Exception("A subcommand must be provided.");

            // No subcommand given or required
            return NonSubcommand ?? new NonSubcommand(string.Empty);
        }

        /// <summary>
        /// Gets parsed command arguments for a given subcommand (or non-subcommand).
        /// </summary>
        /// <param name="args">Argument strings from the command line.</param>
        /// <param name="subcommand">Subcommand (or non-subcommand) to parse for.</param>
        /// <returns>Parsed command arguments.</returns>
        /// <exception cref="Exception">Thrown in case of missing or excessive arguments.</exception>
        private CommandArgs GetCommandArgs(IEnumerable<string> args, Subcommand subcommand)
        {
            // If invoked with a subcommand, skip the first argument, since it was the subcommand
            if (subcommand.IsSubcommand)
                args = args.Skip(1);

            var argList = new List<KeyValuePair<string, object>>();

            // Get required parameters
            var requiredParams = subcommand.RequiredParameters;
            if (args.Count() < requiredParams.Count)
            {
                var missingArgCount = requiredParams.Count - args.Count();

                if (subcommand.IsSubcommand)
                    throw new Exception($"Missing {missingArgCount} required {subcommand.Name} argument{(missingArgCount > 1 ? "s" : string.Empty)}.");
                else
                    throw new Exception($"Missing {missingArgCount} required argument{(missingArgCount > 1 ? "s" : string.Empty)}.");
            }

            // Add arguments for required parameters
            var argIdx = 0;
            foreach (var arg in args.Take(requiredParams.Count))
            {
                var value = GetArgValue(requiredParams[argIdx], arg);
                argList.Add(new KeyValuePair<string, object>(requiredParams[argIdx].name, value));
                argIdx++;
            }

            // Get optional parameters
            var optionalParams = subcommand.OptionalParameters;

            // Add arguments for optional parameters
            argIdx = 0;
            var argsForOptionalParams = args.Skip(requiredParams.Count).ToList();
            foreach (var arg in argsForOptionalParams)
            {
                if (argIdx >= optionalParams.Count)
                {
                    var excessArgCount = argsForOptionalParams.Count() - optionalParams.Count;

                    if (subcommand.IsSubcommand)
                        throw new Exception($"{excessArgCount} too many {subcommand.Name} arguments.");
                    else
                        throw new Exception($"{excessArgCount} too many arguments.");
                }

                var value = GetArgValue(optionalParams[argIdx], arg);
                argList.Add(new KeyValuePair<string, object>(optionalParams[argIdx].name, value));
                argIdx++;
            }

            return new CommandArgs(argList);
        }

        /// <summary>
        /// Gets parsed command options for a given subcommand (or non-subcommand).
        /// </summary>
        /// <param name="options">Option name/value strings from the command line.</param>
        /// <param name="subcommand">Subcommand (or non-subcommand) to parse for.</param>
        /// <returns>Parsed command options.</returns>
        /// <exception cref="Exception">Thrown in case of missing, invalid or excessive options.</exception>
        private CommandOptions GetCommandOptions(IDictionary<string, string> options, Subcommand subcommand)
        {
            var optionList = new List<KeyValuePair<string, object>>();

            // Get required options
            var requiredOptions = subcommand.RequiredOptions;

            // Add items for required options
            foreach (var option in requiredOptions)
            {
                var item = options.FirstOrDefault(kv => string.Equals(kv.Key, option.Key, StringComparison.OrdinalIgnoreCase));

                if (item.Key is null)
                {
                    if (subcommand.IsSubcommand)
                        throw new Exception($"Missing required {subcommand.Name} option '-{option.Key}'.");
                    else
                        throw new Exception($"Missing required option '-{option.Key}'.");
                }

                if (option.Value.hasValue && item.Value == null)
                {
                    if (subcommand.IsSubcommand)
                        throw new Exception($"{subcommand.Name} option '-{option.Key}' is missing a value.");
                    else
                        throw new Exception($"Option '-{option.Key}' is missing a value.");
                }

                if (!option.Value.hasValue && item.Value != null)
                {
                    if (subcommand.IsSubcommand)
                        throw new Exception($"{subcommand.Name} option '-{option.Key}' is not supposed to have a value.");
                    else
                        throw new Exception($"Option '-{option.Key}' is missing a value.");
                }

                var value = GetOptionValue(option, item.Value);
                optionList.Add(new KeyValuePair<string, object>(option.Key, value));
            }

            // Get optional options
            var optionalOptions = subcommand.OptionalOptions;

            // Add items for optional options
            foreach (var option in optionalOptions)
            {
                var item = options.FirstOrDefault(kv => string.Equals(kv.Key, option.Key, StringComparison.OrdinalIgnoreCase));

                if (!(item.Key is null))
                {
                    if (option.Value.hasValue && item.Value == null)
                    {
                        if (subcommand.IsSubcommand)
                            throw new Exception($"{subcommand.Name} option '-{option.Key}' is missing a value.");
                        else
                            throw new Exception($"Option '-{option.Key}' is missing a value.");
                    }

                    if (!option.Value.hasValue && item.Value != null)
                    {
                        if (subcommand.IsSubcommand)
                            throw new Exception($"{subcommand.Name} option '-{option.Key}' is not supposed to have a value.");
                        else
                            throw new Exception($"Option '-{option.Key}' is not supposed to have a value.");
                    }

                    var value = GetOptionValue(option, item.Value);
                    optionList.Add(new KeyValuePair<string, object>(option.Key, value));
                }
            }

            // Check for unexpected options
            foreach (var option in options)
            {
                if (!requiredOptions.ContainsKey(option.Key) && !optionalOptions.ContainsKey(option.Key))
                {
                    if (subcommand.IsSubcommand)
                        throw new Exception($"Unexpected {subcommand.Name} option '-{option.Key}'.");
                    else
                        throw new Exception($"Unexpected option '-{option.Key}'.");
                }
            }

            return new CommandOptions(optionList);
        }

        /// <summary>
        /// Gets the value for a given argument. 
        /// </summary>
        /// <param name="parameter">Parameter definition, including delegate for retrieving the argument value.</param>
        /// <param name="inputValue">Given argument string.</param>
        /// <returns>Value object for the argument.</returns>
        /// <exception cref="Exception">An error occurred when retrieving the value for the argument.</exception>
        private object GetArgValue((string name, string description, Func<string, object> toObject) parameter, string inputValue)
        {
            try
            {
                return parameter.toObject(inputValue ?? string.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception($"{parameter.name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the value for a given option. 
        /// </summary>
        /// <param name="parameter">Option definition, including delegate for retrieving the option value.</param>
        /// <param name="inputValue">Given value string.</param>
        /// <returns>Value object for the option.</returns>
        /// <exception cref="Exception">An error occurred when retrieving the value for the option.</exception>
        private object GetOptionValue(KeyValuePair<string, (string description, bool hasValue, Func<string, object> toObject)> option, string inputValue)
        {
            try
            {
                return option.Value.toObject(inputValue ?? string.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception($"-{option.Key}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the general help text for the command.
        /// </summary>
        /// <param name="context">Command execution context.</param>
        /// <returns>Help text to be shown.</returns>
        private string GetGeneralHelpText(ICommandExecutionContext context)
        {
            var lines = new List<string>();

            lines.Add(this.Description);
            lines.Add(string.Empty);

            if (this.Subcommands.Any())
            {
                if (this.NonSubcommand != null)
                {
                    string description;

                    if (string.IsNullOrEmpty(this.NonSubcommand.Description))
                        description = "Without using a subcommand:";
                    else
                        description = $"Without using a subcommand: {this.NonSubcommand.Description}";

                    lines.AddRange(GetSubcommandHelpTextLines(null, description));
                    lines.Add(string.Empty);
                }

                lines.Add("Subcommands:");
                lines.AddRange(TextFormatting.GetAlignedColumnStrings(this.Subcommands.OrderBy(subcmd => subcmd.Key).Select(kv => kv.Value),
                                                                      ": ",
                                                                      ("", subcmd => $"  {subcmd.Name}", TextAlignment.Start),
                                                                      ("", subcmd => subcmd.Description, TextAlignment.Start)));

                lines.Add(string.Empty);
                lines.Add($"Type '{context.Command.Name} {this.Name} <subcommand-name>' to get detailed help for a subcommand.");
            }
            else
            {
                // Don't show any non-subcommand description if there are no subcommands.
                var nonSubcommandDescriptionToShow = this.Subcommands.Any() ? this.NonSubcommand.Description : string.Empty;

                lines.AddRange(GetSubcommandHelpTextLines(null, nonSubcommandDescriptionToShow));
            }

            return string.Join(context.Output.LineTermination, lines.ToArray());
        }

        /// <summary>
        /// Gets the help text for a specific subcommand.
        /// </summary>
        /// <param name="context">Command execution context.</param>
        /// <param name="subcommandName">Name of the subcommand.</param>
        /// <returns>Help text to be shown.</returns>
        private string GetSubcommandHelpText(ICommandExecutionContext context, string subcommandName)
        {
            return string.Join(context.Output.LineTermination, GetSubcommandHelpTextLines(subcommandName).ToArray());
        }

        /// <summary>
        /// Gets the help text lines for a specific subcommand.
        /// </summary>
        /// <param name="subcommandName">Name of the subcommand.</param>
        /// <param name="customDescription">Optional custom subcommand description text.</param>
        /// <returns>Help text lines to be shown.</returns>
        private IEnumerable<string> GetSubcommandHelpTextLines(string subcommandName, string customDescription = null)
        {
            var subcommand = string.IsNullOrEmpty(subcommandName)
                ? this.NonSubcommand
                : this.Subcommands.FirstOrDefault(subcmd => subcmd.Key.Equals(subcommandName, StringComparison.InvariantCultureIgnoreCase)).Value;

            // No subcommand found?
            if (subcommand is null)
            {
                if (subcommandName.Any(ch => char.IsWhiteSpace(ch)))
                    throw new Exception($"Empty or invalid subcommand '{subcommandName}'.");
                else
                    throw new Exception($"Unknown subcommand '{subcommandName}'.");
            }

            var lines = new List<string>();

            var description = customDescription ?? (!string.IsNullOrEmpty(subcommand.Description) ? subcommand.Description : this.Description);

            if (!string.IsNullOrEmpty(description))
            {
                lines.Add(description);
                lines.Add(string.Empty);
            }

            string synopsis;
            if (subcommand.IsSubcommand)
            {
                // A subcommand
                synopsis = $"{this.Name} {subcommand.Name}";
            }
            else
            {
                // Just the command itself (using no subcommand)
                synopsis = this.Name;
            }

            var options = new List<(string text, bool required)>();

            foreach (var option in subcommand.RequiredOptions)
            {
                if (option.Value.hasValue)
                    synopsis = synopsis + $" -{option.Key}=<value>";
                else
                    synopsis = synopsis + $" -{option.Key}";
            }

            foreach (var option in subcommand.OptionalOptions)
            {
                if (option.Value.hasValue)
                    synopsis = synopsis + $" [-{option.Key}=<value>]";
                else
                    synopsis = synopsis + $" [-{option.Key}]";
            }

            foreach (var parameter in subcommand.RequiredParameters)
                synopsis = synopsis + $" <{parameter.name}>";

            foreach (var parameter in subcommand.OptionalParameters)
                synopsis = synopsis + $" [<{parameter.name}>]";

            lines.Add(synopsis);

            if (subcommand.RequiredParameters.Any() || subcommand.OptionalParameters.Any())
            {
                lines.Add(string.Empty);

                lines.AddRange(TextFormatting.GetAlignedColumnStrings(subcommand.RequiredParameters.Union(subcommand.OptionalParameters).ToList(),
                                                                      ": ",
                                                                      ("", p => $"  {p.name}", TextAlignment.Start),
                                                                      ("", p => p.description, TextAlignment.Start)));
            }

            if (subcommand.RequiredOptions.Any() || subcommand.OptionalOptions.Any())
            {
                lines.Add(string.Empty);
                lines.Add("Options:");

                lines.AddRange(TextFormatting.GetAlignedColumnStrings(subcommand.RequiredOptions.Union(subcommand.OptionalOptions).OrderBy(opt => opt.Key).ToList(),
                                                                      ": ",
                                                                      ("", kv => $"  -{kv.Key}",       TextAlignment.Start),
                                                                      ("", kv => kv.Value.description, TextAlignment.Start)));
            }

            return lines;
        }

        /// <summary>
        /// Defines a required parameter in case the command is executed without a subcommand.
        /// </summary>
        /// <param name="name">Name of the parameter.</param>
        /// <param name="description">Short description of the parameter.</param>
        /// <param name="toObject">A delegate to retrieve the argument value, given an argument string.</param>
        protected void HasRequiredParameter(string name, string description, Func<string, object> toObject)
            => GetOrAddNonSubcommand().HasRequiredParameter(name, description, toObject);

        /// <summary>
        /// Defines an optional parameter in case the command is executed without a subcommand.
        /// </summary>
        /// <param name="name">Name of the parameter.</param>
        /// <param name="description">Short description of the parameter.</param>
        /// <param name="toObject">A delegate to retrieve the argument value, given an argument string.</param>
        protected void HasOptionalParameter(string name, string description, Func<string, object> toObject)
            => GetOrAddNonSubcommand().HasOptionalParameter(name, description, toObject);

        /// <summary>
        /// Defines a required value option in case the command is executed without a subcommand.
        /// </summary>
        /// <param name="name">Name of the option.</param>
        /// <param name="description">Short description of the option.</param>
        /// <param name="toObject">A delegate to retrieve the option value, given a value string.</param>
        protected void HasRequiredValueOption(string name, string description, Func<string, object> toObject)
            => GetOrAddNonSubcommand().HasRequiredValueOption(name, description, toObject);

        /// <summary>
        /// Defines an optional value option in case the command is executed without a subcommand.
        /// </summary>
        /// <param name="name">Name of the option.</param>
        /// <param name="description">Short description of the option.</param>
        /// <param name="toObject">A delegate to retrieve the option value, given a value string.</param>
        protected void HasValueOption(string name, string description, Func<string, object> toObject)
            => GetOrAddNonSubcommand().HasValueOption(name, description, toObject);

        /// <summary>
        /// Defines an optional flag in case the command is executed without a subcommand.
        /// </summary>
        /// <param name="name">Name of the option.</param>
        /// <param name="description">Short description of the option.</param>
        protected void HasFlagOption(string name, string description)
            => GetOrAddNonSubcommand().HasFlagOption(name, description);

        /// <summary>
        /// Defines that the command can be executed without a subcommand.
        /// This is implicit if non-subcommand parameters or options are defined, or if no subcommands are defined.
        /// </summary>
        /// <param name="description">Short description of how the command works when executed without a subcommand.</param>
        /// <returns>The defined non-subcommand.</returns>
        protected Subcommand HasNonSubcommand(string description)
        {
            if (description == null) throw new ArgumentNullException(nameof(description));

            if (_nonSubcommand != null)
                throw new Exception("Non-subcommand was already added.");

            _nonSubcommand = new NonSubcommand(description);

            return _nonSubcommand;
        }

        /// <summary>
        /// Defines that the command can be executed with a given subcommand.
        /// </summary>
        /// <param name="name">Name of the subcommand.</param>
        /// <param name="description">Short description of how the command works when executed with this subcommand.</param>
        /// <returns>The defined subcommand.</returns>
        protected Subcommand HasSubcommand(string name, string description)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing subcommand name.", nameof(name));
            if (name.Any(ch => char.IsWhiteSpace(ch) || char.IsControl(ch))) throw new ArgumentException($"Invalid subcommand name '{name}'.", nameof(name));

            if (description == null) throw new ArgumentNullException(nameof(description));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Missing subcommand description.", nameof(description));

            if (_subcommands.ContainsKey(name))
                throw new ArgumentException($"Subcommand '{name}' was already added.", nameof(name));

            var subcommand = new Subcommand(name, description);

            _subcommands.Add(name, subcommand);

            return subcommand;
        }

        /// <summary>
        /// Gets or adds the non-subcommand definition.
        /// </summary>
        /// <returns>The defined non-subcommand.</returns>
        private Subcommand GetOrAddNonSubcommand()
        {
            if (_nonSubcommand == null)
                _nonSubcommand = new NonSubcommand(string.Empty);

            return _nonSubcommand;
        }

        /// <summary>
        /// Helper method to get an exception message text for an unknown subcommand.
        /// </summary>
        /// <param name="subcommandName">Given unknown subcommand.</param>
        /// <param name="candidates">Possible subcommands.</param>
        /// <returns>Exception message text.</returns>
        private string GetUnknownSubcommandExceptionText(string subcommandName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Unknown subcommand '{subcommandName}'. Possible subcommands:");

            var candidates = new List<Subcommand>();
            
            if (NonSubcommand != null)
                candidates.Add(NonSubcommand);

            candidates.AddRange(Subcommands.Values.OrderBy(subcmd => subcmd.Name));
                        
            foreach (var line in TextFormatting.GetAlignedColumnStrings(candidates,
                                                                        " ",
                                                                        ("", subcmd => $"  {(subcmd.IsSubcommand ? subcmd.Name : "(none)")}:", TextAlignment.Start),
                                                                        ("", subcmd => subcmd.Description, TextAlignment.Start)))
            {
                sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd(new[] { '\r', '\n' });
        }
    }
}
