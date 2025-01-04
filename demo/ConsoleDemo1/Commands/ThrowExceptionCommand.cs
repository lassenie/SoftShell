using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDemo1.Commands
{
    internal class ThrowExceptionCommand : StdCommand
    {
        public override string Name => "Throw";

        public override string Description => "Throws an exception which will be directed to the error output.";

        public ThrowExceptionCommand()
        {
            HasOptionalParameter("message", "Optional message for the exception.", val => val);
            HasValueOption("stdout", "Text to write to standard output (before/after eventual error output depending on order).", val => val);
            HasValueOption("stderr", "Text to write to standard error output (before/after eventual standard output depending on order).", val => val);
        }

        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            int? stdOutIndex = null;
            int? stdErrIndex = null;

            if (options.TryGetAs("stdout", out string stdOutText))
                stdOutIndex = commandLine.IndexOf("stdout=", StringComparison.InvariantCultureIgnoreCase);

            if (options.TryGetAs("stderr", out string stdErrText))
                stdErrIndex = commandLine.IndexOf("stderr=", StringComparison.InvariantCultureIgnoreCase);

            // Write to output first?
            if (stdOutIndex.HasValue && (!stdErrIndex.HasValue || stdOutIndex.Value < stdErrIndex.Value))
                await context.Output.WriteLineAsync(stdOutText).ConfigureAwait(false);

            // Write to error output?
            if (stdErrIndex.HasValue)
                await context.ErrorOutput.WriteLineAsync(stdErrText).ConfigureAwait(false);

            // Write to output after error output?
            if (stdOutIndex.HasValue && stdErrIndex.HasValue && stdOutIndex.Value > stdErrIndex.Value)
                await context.Output.WriteLineAsync(stdOutText).ConfigureAwait(false);

            if (args.TryGetAs<string>(0, out var message))
                throw new Exception(message);
            else
                throw new Exception();
        }
    }
}
