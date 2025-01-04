using SoftShell;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest
{
    public abstract class TestCommand : StdCommand
    {
        private Action<int, Type> _logExecution;
        private Action<Exception> _logException;

        protected TestCommand(Action<int, Type> logExecution, Action<Exception> logException)
        {
            _logExecution = logExecution;
            _logException = logException;
        }

        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            _logExecution(context.CommandChainIndex, this.GetType());
            try
            {
                TestExecute(context, subcommand, args, options, commandLine);
                return Task.CompletedTask;
            }
            catch (System.Exception ex)
            {
                _logException(ex);
                throw;
            }
        }

        protected abstract void TestExecute(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine);
    }
}
