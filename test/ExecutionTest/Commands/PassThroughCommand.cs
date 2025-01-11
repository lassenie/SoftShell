using SoftShell;
using SoftShell.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class PassThroughCommand : TestCommand
    {
        protected override string Name => "passthrough";

        public override string Description => "Passing through any output.";

        public PassThroughCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            while (!context.Input.IsEnded)
            {
                var str = context.Input.ReadAsync().Result;

                if (str != null)
                    context.Output.WriteAsync(str).Wait();
            }
        }
    }
}
