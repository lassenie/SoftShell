using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class SubcmdAndNonSubcmdCommand : TestCommand
    {
        public override string Name => "SubcmdAndNonSubcmd";

        public override string Description => "Testing a command with subcommands and possibility to execute without.";

        public SubcmdAndNonSubcmdCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
            HasRequiredValueOption("requiredvaloption", "Required value option if using no subcommand.", val => val);

            HasSubcommand("subcmd", "Subcommand description.")
                .HasRequiredParameter("requiredparam", "Required parameter for subcommand.", val => val);
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            switch (subcommand.Name)
            {
                case "":
                    Assert.Empty(args);
                    Assert.Single(options);
                    Assert.Equal("requiredvaloption", options.First().name);
                    Assert.True(options.First().value is string);
                    context.Output.WriteAsync(options["requiredvaloption"].ToString()).Wait();
                    break;

                case "subcmd":
                    Assert.Single(args);
                    Assert.Equal("requiredparam", args.First().name);
                    Assert.Empty(options);
                    context.Output.WriteAsync(args[0].ToString()).Wait();
                    break;

                default:
                    throw new Exception("Unsupported subcommand - should not come to this point.");
            }
        }
    }
}
