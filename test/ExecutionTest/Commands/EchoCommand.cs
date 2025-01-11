using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class EchoCommand : TestCommand
    {
        protected override string Name => "echo";

        public override string Description => "Writes a given text as a line to the output.";

        public EchoCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
            HasRequiredParameter("text", "The text to write.", val => val);
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            context.Output.WriteLineAsync(args.GetAs<string>(0)).Wait();
        }
    }
}
