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
        /// <summary>
        /// Type for passing an abstract process object (supports unit testing).
        /// </summary>
        public class ProcessObj : AbstractionObj { public ProcessObj(object process) : base(process) { } }

        /// <summary>
        /// Host interface for accessing the application's process. Can be mocked for unit testing.
        /// </summary>
        public interface IHost
        {
            /// <summary>
            /// Gets the application's process as an abstract object.
            /// Wraps <see cref="Process.GetCurrentProcess"/> in the default implementation.
            /// </summary>
            ProcessObj GetCurrentProcess();

            /// <summary>Gets the id of a given process.</summary>
            int GetId(ProcessObj process);

            /// <summary>Gets the session id of a given process.</summary>
            int GetSessionId(ProcessObj process);

            /// <summary>Gets the name of a given process.</summary>
            string GetProcessName(ProcessObj process);

            /// <summary>Gets the main window title of a given process (empty if none).</summary>
            string GetMainWindowTitle(ProcessObj process);

            /// <summary>Gets the file name of the main module of a given process.</summary>
            string GetMainModuleFileName(ProcessObj process);

            /// <summary>Gets the amount of physical memory, in bytes, allocated for a given process.</summary>
            long GetWorkingSet64(ProcessObj process);

            /// <summary>Gets the priority class of a given process as text.</summary>
            string GetPriorityClass(ProcessObj process);

            /// <summary>Gets the base priority of a given process.</summary>
            int GetBasePriority(ProcessObj process);

            /// <summary>Gets the number of handles opened by a given process.</summary>
            int GetHandleCount(ProcessObj process);

            /// <summary>
            /// Gets the command-line arguments for the application (including the entry executable/DLL as the first element).
            /// Wraps <see cref="Environment.GetCommandLineArgs"/> in the default implementation.
            /// </summary>
            IEnumerable<string> GetCommandLineArgs();

            /// <summary>
            /// Gets the current working directory of the application.
            /// Wraps <see cref="Directory.GetCurrentDirectory"/> in the default implementation.
            /// </summary>
            string GetCurrentDirectory();

            /// <summary>
            /// Kills a given process.
            /// Wraps <see cref="Process.Kill()"/> in the default implementation.
            /// </summary>
            void Kill(ProcessObj process);
        }

        private IHost _host;

        public override string Description => "Handling of the application's process";

        protected override string Name => "process";

        /// <summary>
        /// Constructor that creates the command object using a default <see cref="IHost"/> implementation.
        /// </summary>
        internal ProcessCommand() : this(new DefaultHost()) { }

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="IHost"/> implementation.
        /// </summary>
        internal ProcessCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

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
            var process = _host.GetCurrentProcess();

            List<string> GetProcessArgs()
            {
                var args = _host.GetCommandLineArgs().Skip(1).ToList(); // First arg is the main process DLL, skip that

                // Ensure at least one line of arguments shown (in that case empty)
                if (!args.Any())
                    args.Add(string.Empty);

                return args;
            }

            string GetAllocatedPhysicalMemoryStr()
            {
                var bytes = _host.GetWorkingSet64(process);

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

            properties.Add(("Id", _host.GetId(process).ToString()));
            properties.Add(("Session", _host.GetSessionId(process).ToString()));
            properties.Add(("Name", _host.GetProcessName(process)));
            properties.Add(("Main window", _host.GetMainWindowTitle(process) ?? string.Empty));
            properties.Add(("File", _host.GetMainModuleFileName(process)));

            var processArgs = GetProcessArgs();

            for (var i = 0; i < processArgs.Count; ++i)
                properties.Add((i == 0 ? "Arguments" : string.Empty, processArgs[i]));

            properties.Add(("Current dir", _host.GetCurrentDirectory()));
            properties.Add(("Priority class", _host.GetPriorityClass(process)));
            properties.Add(("Base priority", _host.GetBasePriority(process).ToString()));
            properties.Add(("Memory, physical", GetAllocatedPhysicalMemoryStr()));
            properties.Add(("Handles", _host.GetHandleCount(process).ToString()));

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
            _host.Kill(_host.GetCurrentProcess());
        }

        private class DefaultHost : IHost
        {
            public ProcessObj GetCurrentProcess() => new ProcessObj(Process.GetCurrentProcess());
            public int GetId(ProcessObj process) => ((Process)process.Object).Id;
            public int GetSessionId(ProcessObj process) => ((Process)process.Object).SessionId;
            public string GetProcessName(ProcessObj process) => ((Process)process.Object).ProcessName;
            public string GetMainWindowTitle(ProcessObj process) => ((Process)process.Object).MainWindowTitle;
            public string GetMainModuleFileName(ProcessObj process) => ((Process)process.Object).MainModule.FileName;
            public long GetWorkingSet64(ProcessObj process) => ((Process)process.Object).WorkingSet64;
            public string GetPriorityClass(ProcessObj process) => ((Process)process.Object).PriorityClass.ToString();
            public int GetBasePriority(ProcessObj process) => ((Process)process.Object).BasePriority;
            public int GetHandleCount(ProcessObj process) => ((Process)process.Object).HandleCount;
            public IEnumerable<string> GetCommandLineArgs() => Environment.GetCommandLineArgs();
            public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
            public void Kill(ProcessObj process) => ((Process)process.Object).Kill();
        }
    }
}
