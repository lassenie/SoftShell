using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Commands
{
    internal class ReverseCommand : TestCommand
    {
        protected override string Name => "reverse";

        public override string Description => "Outputting reversed input.";

        public ReverseCommand(Action<int, Type> logExecution, Action<Exception> logException) : base(logExecution, logException)
        {
        }

        protected override void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            var sb = new StringBuilder();

            while (!context.Input.IsEnded)
            {
                var str = context.Input.ReadAsync().Result;

                if (str != null)
                    sb.Append(str);
            }

            context.Output.WriteAsync(new string(sb.ToString().Reverse().ToArray())).Wait();
        }
    }
}
