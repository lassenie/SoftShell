using SoftShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleDemo1.Commands
{
    public sealed class DelayCommand : StdCommand
    {
        public interface IHost
        {
            Task DelayAsync(int seconds, CancellationToken cancelToken);
        }

        private IHost _host;

        public override string Name => "Delay";

        public override string Description => "Waits for a given number of seconds before processing piped input, if any, and finishing.";

        public DelayCommand() : this(new DefaultHost()) { }

        public DelayCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            HasRequiredParameter("duration", "Delay time in seconds",
                                 str => int.TryParse(str, NumberStyles.None, CultureInfo.InvariantCulture, out int val)
                                     ? val : throw new Exception("Please provide a positive integer for the delay time."));
        }

        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            // Wait for the given number of seconds
            await _host.DelayAsync(args.GetAs<int>(0), context.CancellationToken).ConfigureAwait(false);

            // Pass through all input, if anything piped
            while (context.Input.IsPiped && !context.Input.IsEnded)
            {
                var str = await context.Input.TryReadAsync().ConfigureAwait(false);

                if (str != null)
                    await context.Output.WriteAsync(str).ConfigureAwait(false);
                else
                    await Task.Delay(10, context.CancellationToken).ConfigureAwait(false);
            }
        }

        private class DefaultHost : IHost
        {
            public Task DelayAsync(int seconds, CancellationToken cancelToken)
                => Task.Delay(seconds * 1000, cancelToken);
        }
    }
}
