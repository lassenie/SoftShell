using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command for providing information about the shell and the host program.
    /// </summary>
    public class InformationCommand : StdCommand
    {
        Func<IEnumerable<string>> _infoFunc;

        /// <inheritdoc/>
        public override string Name => "Info";

        /// <inheritdoc/>
        public override string Description => "Shows info about the shell and the host program.";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="infoFunc">
        /// Delegate for providing information to the user about the app and SoftShell.
        /// Returns: Lines of text to show (null or empty collection if nothing).
        /// </param>
        internal InformationCommand(Func<IEnumerable<string>> infoFunc)
        {
            _infoFunc = infoFunc;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            var infoLines = _infoFunc?.Invoke() ?? Enumerable.Empty<string>();

            foreach (var line in infoLines)
                if (line != null) // Allow empty line but ignore null
                    await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
