using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class ThrowExceptionCommand : TestCommand
    {
        protected override string Name => "throw";

        public override string Description => "Command that throws an exception";

        public ThrowExceptionCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
            HasOptionalParameter("message", "Optional exception message", val => val);
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (args.TryGetAs<string>(0, out var message))
                throw new System.Exception(message);
            else
                throw new System.Exception();
        }
    }
}
