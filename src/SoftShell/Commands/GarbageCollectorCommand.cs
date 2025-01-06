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
    /// Command for using the .NET garbage collector. See command help info for further details.
    /// </summary>
    public class GarbageCollectorCommand : StdCommand
    {
        /// <summary>
        /// Host interface for accessing the garbage collector. Can be mocked for unit testing.
        /// </summary>
        public interface IHost
        {
            /// <summary>
            /// Performs garbage collection.
            /// Wraps the <see cref="GC.Collect()"/> method in the default implementation.
            /// </summary>
            void GC_Collect();

            /// <summary>
            /// Attempts to disallow garbage collection.
            /// Wraps the <see cref="GC.TryStartNoGCRegion(long)"/> method in the default implementation.
            /// </summary>
            /// <returns>True if successfully started disallowing garbage collection.</returns>
            bool GC_StartNoGCRegion(long totalsize);

            /// <summary>
            /// Ends the no GC region latency mode.
            /// Wraps the <see cref="GC.EndNoGCRegion"/> method in the default implementation.
            /// </summary>
            /// <returns>True if ended disallowing garbage collection, or false if it wasn't disallowed.</returns>
            bool GC_EndNoGCRegion();
        }

        private IHost _host;

        /// <inheritdoc/>
        public override string Name => "GC";

        /// <inheritdoc/>
        public override string Description => "Interacts with the .NET garbage collector.";

        /// <summary>
        /// Constructor that creates the command object using a default <see cref="IHost"/> implementation.
        /// </summary>
        internal GarbageCollectorCommand() : this(new DefaultHost()) { }

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="IHost"/> implementation.
        /// </summary>
        internal GarbageCollectorCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            HasSubcommand("collect",   "Forces immediate garbage collection.");
            HasSubcommand("disable", "Attempts to disallow garbage collection.")
                .HasRequiredValueOption("totalsize", "The amount of memory in bytes to allocate without triggering a garbage collection (postfix with 'K' or 'M' for kilobytes or megabytes).", val => val);
            HasSubcommand("enable",   "Re-allows garbage collection.");
        }

        /// <inheritdoc/>
        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            switch (subcommand.Name)
            {
                case "collect":
                    return CollectGarbageAsync(context);

                case "disable":
                    return NoGcStartAsync(context);

                case "enable":
                    return NoGcEndAsync(context);

                default:
                    throw new Exception($"Unhandled or missing subcommand '{subcommand.Name}'.");
            }
        }

        private async Task CollectGarbageAsync(IStdCommandExecutionContext context)
        {
            await context.Output.WriteLineAsync("Collecting garbage...");

            await Task.Run(() => _host.GC_Collect()).ConfigureAwait(false);
            await context.Output.WriteLineAsync("Garbage collected.").ConfigureAwait(false);
        }

        private async Task NoGcStartAsync(IStdCommandExecutionContext context)
        {
            long? GetTotalSize()
            {
                var totalSizeStr = context.Options.Get("totalsize")?.ToString().Trim().ToUpperInvariant() ?? string.Empty;

                var regexMatch = Regex.Match(totalSizeStr, @"^(\d+)([KM]?)$");

                if (!regexMatch.Success || regexMatch.Groups.Count != 3)
                    return null;

                var value = long.Parse(regexMatch.Groups[1].Value);

                switch (regexMatch.Groups[2].Value)
                {
                    case "K": return value * 1024L;
                    case "M": return value * 1048576L;
                    case "": return value;
                }

                return null;
            }

            var totalSize = GetTotalSize() ?? throw new Exception("Invalid 'totalsize' value.");

            await context.Output.WriteLineAsync("Disabling garbage collection...").ConfigureAwait(false);

            if (await Task.Run(() => _host.GC_StartNoGCRegion(totalSize)).ConfigureAwait(false))
            {
                await context.Output.WriteLineAsync("Now disabled until enabled by the 'gc enable' command or the host program itself, or until the given max. number of allocated bytes are exceeded.").ConfigureAwait(false);
            }
            else
            {
                await context.ErrorOutput.WriteLineAsync("Not possible to disable garbage collection - perhap already disabled.").ConfigureAwait(false);
            }
        }

        private async Task NoGcEndAsync(IStdCommandExecutionContext context)
        {
            await context.Output.WriteLineAsync("Enabling garbage collection...").ConfigureAwait(false);

            if (await Task.Run(() => _host.GC_EndNoGCRegion()).ConfigureAwait(false))
            {
                await context.Output.WriteLineAsync("Garbage collection enabled.").ConfigureAwait(false);
            }
            else
            {
                await context.ErrorOutput.WriteLineAsync("Not possible to enable garbage collection - perhaps already enabled.").ConfigureAwait(false);
            }
        }

        private class DefaultHost : IHost
        {
            public void GC_Collect()
            {
                GC.Collect();
            }

            public bool GC_StartNoGCRegion(long totalsize)
            {
                try
                {
                    return GC.TryStartNoGCRegion(totalsize);
                }
                catch (InvalidOperationException)
                {
                    // May already be started
                    return false;
                }
            }

            public bool GC_EndNoGCRegion()
            {
                try
                {
                    GC.EndNoGCRegion();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // May already be ended
                    return false;
                }
            }
        }
    }
}
