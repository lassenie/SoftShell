using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class SubcmdOnlyCommand : TestCommand
    {
        public override string Name => "SubcmdOnly";

        public override string Description => "Testing a command with subcommands but no possibility to execute without.";

        public SubcmdOnlyCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
            HasSubcommand("subcmd1", "Subcommand 1 description.")
                .HasRequiredParameter("requiredparam", "Required parameter for subcommand 1.", val => val)
                .HasValueOption("valueoption", "Optional value", val => val);

            HasSubcommand("subcmd2", "Subcommand 2 description.")
                .HasOptionalParameter("optionalparam", "Optional parameter for subcommand 2.", val => val)
                .HasRequiredValueOption("requiredvaloption", "Required value option for subcommand 2.", val => val)
                .HasFlagOption("flagoption", "Flag option for subcommand 2.");
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            switch (subcommand.Name)
            {
                case "":
                    throw new Exception("Subcommand required - should not come to this point.");

                case "subcmd1":
                    Assert.Single(args);
                    Assert.Equal("requiredparam", args.First().name);
                    context.Output.WriteAsync(args[0].ToString()).Wait();
                    Assert.True(options.Count() <= 1);
                    if (options.Any())
                    {
                        Assert.Equal("valueoption", options.First().name);
                        Assert.True(options.First().value is string);
                        context.Output.WriteAsync(options["valueoption"].ToString()).Wait();
                    }
                    break;

                case "subcmd2":
                    Assert.True(args.Count() <= 1);
                    if (args.Any())
                    {
                        Assert.Equal("optionalparam", args.First().name);
                        context.Output.WriteAsync(args[0].ToString()).Wait();
                    }
                    Assert.True(options.Count() >= 1 && options.Count() <= 2);
                    Assert.Equal("requiredvaloption", options.First().name);
                    Assert.True(options.First().value is string);
                    context.Output.WriteAsync(options["requiredvaloption"].ToString()).Wait();
                    if (options.Count() > 1)
                    {
                        Assert.Equal("flagoption", options.Skip(1).First().name);
                        Assert.True(options.Skip(1).First().value is bool);
                        context.Output.WriteAsync("flagoption").Wait();
                    }
                    break;

                default:
                    throw new Exception("Unsupported subcommand - should not come to this point.");
            }
        }
    }
}
