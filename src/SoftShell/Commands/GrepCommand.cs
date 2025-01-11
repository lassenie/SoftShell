using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command for searching for patterns in a given file or piped input and outputs matching lines.
    /// </summary>
    public class GrepCommand : StdCommand
    {
        /// <inheritdoc/>
        protected override string Name => "grep";

        /// <inheritdoc/>
        public override string Description => "Searches for one or more patterns in input lines and outputs lines with matching patterns.";

        /// <summary>
        /// Constructor that creates the command object.
        /// </summary>
        internal GrepCommand()
        {
            HasRequiredParameter("patterns", "Regular expression(s) to use for matching (separate with '\\|' if multiple).", val => val);

            HasFlagOption("ignorecase", "Ignores casing in patterns and input data.");
            HasFlagOption("invertmatch", "Selects the non-matching lines rather than the matching lines.");
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (!args.TryGetAs<string>(0, out var patterns))
                throw new Exception($"Missing patterns argument.");

            var invertMatch = options.HasFlag("invertmatch");
            var ignoreCase = options.HasFlag("ignorecase");

            var regexOptions = RegexOptions.CultureInvariant | RegexOptions.Singleline;

            if (ignoreCase)
                regexOptions |= RegexOptions.IgnoreCase;

            var regexes = patterns.Split(new string[] { @"\|" }, StringSplitOptions.None).Select(pattern => new Regex(pattern, regexOptions)).ToList();

            while (!context.Input.IsEnded)
            {
                var line = await context.Input.ReadLineAsync();

                if (regexes.Any(regex => regex.IsMatch(line)) ^ invertMatch)
                    await context.Output.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
    }
}
