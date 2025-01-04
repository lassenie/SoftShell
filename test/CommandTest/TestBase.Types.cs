using SoftShell.Exceptions;
using SoftShell.IO;
using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SoftShell.Execution;

namespace CommandTest
{
    public partial class TestBase
    {
        private class TestSession : ISession
        {
            public int Id => 1;

            private ISessionHost _host;
            public ISessionHost Host => _host;
            public bool IsEnded => false;
            public SessionState State => SessionState.Running;

            public int? TerminalWindowWidth => throw new NotImplementedException();

            public int? TerminalWindowHeight => throw new NotImplementedException();

            public string TerminalInstanceInfo => throw new NotImplementedException();

            public string TerminalType => throw new NotImplementedException();

            public TestSession()
            {
                _host = new TestHost();
            }

            public void RequestEnd() => throw new NotImplementedException();
        }

        private class TestHost : ISessionHost
        {
            public IUserAuthentication UserAuthentication => throw new NotImplementedException();

            public IEnumerable<Command> Commands => throw new NotImplementedException();

            public void AddCommand(string groupPrefix, string groupName, Command command)
            {
                throw new NotImplementedException();
            }

            public void AddCommands(string groupPrefix, string groupName, Assembly assembly)
            {
                throw new NotImplementedException();
            }

            public ISession CreateSession(ITerminalInterface terminal)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> GetSessionStartInfo()
            {
                throw new NotImplementedException();
            }

            public void TerminateSession(int sessionId, bool hard)
            {
                // Do nothing
            }
        }

        private class TestErrorOutput : ICommandExceptionOutput
        {
            ITestExceptionHandler _exceptionHandler;

            public TestErrorOutput(ITestExceptionHandler exceptionHandler)
            {
                _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            }

            public Task HandleExceptionAsync(CommandException exception)
            {
                _exceptionHandler.HandleException(exception);

                return Task.CompletedTask;
            }
        }

        public interface ITestExceptionHandler
        {
            void HandleException(Exception exception);
        }
    }
}
