using Moq;
using SoftShell;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SoftShell.Commands.SessionCommand;

namespace CommandTest.Tests
{
    public class SessionCommandTest : TestBase
    {
        ISession[] noSessions = new ISession[0];
        ISession[] oneSession = new ISession[]
        {
            new TestSession { Id = 1, State = SessionState.Running, TerminalWindowWidth = 1, TerminalWindowHeight = 2, TerminalType = "Console", TerminalInstanceInfo = "Local" }
        };
        ISession[] twoSessions = new ISession[]
        {
            new TestSession { Id = 1, State = SessionState.Running, TerminalWindowWidth = 1, TerminalWindowHeight = 2, TerminalType = "Console", TerminalInstanceInfo = "Local" },
            new TestSession { Id = 2, State = SessionState.Login, TerminalWindowWidth = 999, TerminalWindowHeight = 1234, TerminalType = "Telnet", TerminalInstanceInfo = "12.34.56.78:23" }
        };

        [Fact]
        public void TestNoSessions()
        {
            var providerMock = SetupProviderMock(noSessions);

            TestCommandWriteLine(new SessionCommand(providerMock.Object),
                                 "session",
                                 "Current SoftShell sessions:",
                                 "ID  State  Type  Terminal  Window size",
                                 "--  -----  ----  --------  -----------");
        }

        [Fact]
        public void TestOneSession()
        {
            var providerMock = SetupProviderMock(oneSession);

            TestCommandWriteLine(new SessionCommand(providerMock.Object),
                                 "session",
                                 "Current SoftShell sessions:",
                                 "  ID  State    Type     Terminal      Window size",
                                 "  --  -----    ----     --------      -----------",
                                 "*  1  Running  Console  Local (this)  1 x 2");
        }

        [Fact]
        public void TestTwoSessions()
        {
            // Normal order by session ID
            var providerMock = SetupProviderMock(twoSessions);

            TestCommandWriteLine(new SessionCommand(providerMock.Object),
                                 "session",
                                 "Current SoftShell sessions:",
                                 "  ID  State    Type     Terminal        Window size",
                                 "  --  -----    ----     --------        -----------",
                                 "*  1  Running  Console  Local (this)    1 x 2",
                                 "   2  Login    Telnet   12.34.56.78:23  999 x 1234");

            // Reverse order by session ID but list still written ordered by ID
            providerMock = SetupProviderMock(twoSessions.Reverse());

            TestCommandWriteLine(new SessionCommand(providerMock.Object),
                                 "session",
                                 "Current SoftShell sessions:",
                                 "  ID  State    Type     Terminal        Window size",
                                 "  --  -----    ----     --------        -----------",
                                 "*  1  Running  Console  Local (this)    1 x 2",
                                 "   2  Login    Telnet   12.34.56.78:23  999 x 1234");
        }

        private class TestSession : ISession
        {
            public int Id { get; set; }

            public ISessionHost Host => throw new NotImplementedException();

            public bool IsEnded => State == SessionState.Ended;

            public SessionState State { get; set; }

            public int? TerminalWindowWidth { get; set; }

            public int? TerminalWindowHeight { get; set; }

            public string TerminalInstanceInfo { get; set; } = string.Empty;

            public string TerminalType { get; set; } = string.Empty;

            public void RequestEnd() => throw new NotImplementedException();
        }

        private Mock<ISessionCollectionProvider> SetupProviderMock(IEnumerable<ISession> sessions)
        {
            var mock = new Mock<ISessionCollectionProvider>(MockBehavior.Strict);

            mock.Setup(mock => mock.GetSessions()).Returns(sessions);

            return mock;
        }
    }
}
