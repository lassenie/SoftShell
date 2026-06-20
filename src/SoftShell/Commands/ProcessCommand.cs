using SoftShell.Helpers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.Commands
{
    public class ProcessCommand : StdCommand
    {
        public override string Description => "Handling of the application's process";

        protected override string Name => "process";

        public ProcessCommand()
        {
            HasNonSubcommand("Shows information about the process.");
            HasSubcommand("kill", "Kills the process.");
        }

        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (!subcommand.IsSubcommand)
                return ListProcessInfoAsync(context);

            switch (subcommand.Name.ToLowerInvariant())
            {
                case "kill":
                    return KillProcessAsync(context);
            }

            throw new Exception($"Unhandled or missing subcommand '{subcommand.Name}'.");
        }

        private async Task ListProcessInfoAsync(IStdCommandExecutionContext context)
        {
            var process = Process.GetCurrentProcess();

            List<string> GetProcessArgs()
            {
                var args = Environment.GetCommandLineArgs().Skip(1).ToList(); // First arg is the main process DLL, skip that

                // Ensure at least one line of arguments shown (in that case empty)
                if (!args.Any())
                    args.Add(string.Empty);

                return args;
            }

            string GetAllocatedPhysicalMemoryStr()
            {
                var bytes = process.WorkingSet64;

                string GetFormattedNumber(long divisor, string unit)
                {
                    const int NeededDigits = 5;

                    // Number of bytes too small?
                    if (bytes < divisor)
                        return null;

                    var value = (double)bytes / divisor;
                    var str = value.ToString();

                    var totalDigits = str.Count(ch => char.IsDigit(ch));
                    var digitsBeforeDecimalPoint = str.TakeWhile(ch => char.IsDigit(ch)).Count();
                    var digitsAfterDecimalPoint = totalDigits - digitsBeforeDecimalPoint;

                    // Remove decimals if we have more significant digits than needed, e.g. if 3 digits needed:
                    // 12345.6 => 12345 (decimals = 0, excess digits = 1), e.g. format string "F0"
                    // 12.3456 => 12.3 (decimals = 1, excess digits = 3), e.g. format string "F1"
                    var excessDigits = Math.Min(totalDigits - NeededDigits, digitsAfterDecimalPoint);
                    digitsAfterDecimalPoint -= excessDigits;

                    var formatString = $"F{digitsAfterDecimalPoint}";

                    return $"{value.ToString(formatString)} {unit}";
                }

                return GetFormattedNumber(1_073_741_824, "GB") ??
                       GetFormattedNumber(1_048_576, "MB") ??
                       GetFormattedNumber(1024, "KB") ??
                       GetFormattedNumber(1, "bytes") ??
                       "0 bytes";
            }

            var properties = new List<(string label, string value)>();

            properties.Add(("Id", process.Id.ToString()));
            properties.Add(("Session", process.SessionId.ToString()));
            properties.Add(("Name", process.ProcessName));
            properties.Add(("Main window", process.MainWindowTitle ?? string.Empty));
            properties.Add(("File", process.MainModule.FileName));

            var processArgs = GetProcessArgs();

            for (var i = 0; i < processArgs.Count; ++i)
                properties.Add((i == 0 ? "Arguments" : string.Empty, processArgs[i]));

            properties.Add(("Current dir", Directory.GetCurrentDirectory()));
            properties.Add(("Priority class", process.PriorityClass.ToString()));
            properties.Add(("Base priority", process.BasePriority.ToString()));
            properties.Add(("Memory, physical", GetAllocatedPhysicalMemoryStr()));
            properties.Add(("Handles", process.HandleCount.ToString()));

            var lines = TextFormatting.GetAlignedColumnStrings(properties,
                                                               ":  ",
                                                               ("", p => p.label, TextAlignment.Start),
                                                               ("", p => p.value ?? "(null)", TextAlignment.Start));

            foreach (var line in lines)
                await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }

        private async Task KillProcessAsync(IStdCommandExecutionContext context)
        {
            await context.Output.WriteLineAsync("Killing the process...");
            await Task.Delay(1000);
            Process.GetCurrentProcess().Kill();
        }
    }
}
