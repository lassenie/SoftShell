using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class ParmsAndOptionsCommand : TestCommand
    {
        public override string Name => "ParmsOptions";

        public override string Description => "Test command with specified arguments/options";

        public ParmsAndOptionsCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
            
        }

        public void HasParamsAndOptions(
            string? subcommand,
            string[] requiredParams,
            string[] optionalParams,
            string[] requiredValueOptions,
            string[] optionalValueOptions,
            string[] flagOptions)
        {
            bool isNonSubcommand = string.IsNullOrEmpty(subcommand);

            // Create the non-subcommand object, if any
            var nonSubcmd = isNonSubcommand ? new Subcommand(string.Empty, string.Empty) : null;

            // Get a reference to a new subcommand (or the non-subcommand) that we will work on
            var subcommandObj = nonSubcmd ?? new Subcommand(subcommand, string.Empty);

            foreach (var param in requiredParams) subcommandObj.HasRequiredParameter(param, string.Empty, val => val);
            foreach (var param in optionalParams) subcommandObj.HasOptionalParameter(param, string.Empty, val => val);
            foreach (var option in requiredValueOptions) subcommandObj.HasRequiredValueOption(option, string.Empty, val => val);
            foreach (var option in optionalValueOptions) subcommandObj.HasValueOption(option, string.Empty, val => val);
            foreach (var option in flagOptions) subcommandObj.HasFlagOption(option, string.Empty);

#pragma warning disable CS8600, CS8602, CS8604

            // Set non-subcommand (may be null if none) - use reflection to access private member
            typeof(StdCommand).GetField("_nonSubcommand", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(this, nonSubcmd);

            // Clear/add subcommand - use reflection to access private member
            var subcommandsField = (Dictionary<string, Subcommand>)typeof(StdCommand).GetField("_subcommands", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(this);
            subcommandsField.Clear();
            if (!isNonSubcommand)
                subcommandsField.Add(subcommand, subcommandObj);

#pragma warning restore CS8600, CS8602, CS8604
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            var sb = new StringBuilder();

            sb.Append(string.Join(' ', args.Select(arg => arg.value)));
            sb.Append(' ');
            sb.Append(string.Join(' ', options.Select(opt => $"-{opt.name}={opt.value}")));

            context.Output.WriteAsync(sb.ToString().Trim()).Wait();
        }
    }
}
