using Moq;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class EnvironmentVariableCommandTest : TestBase
    {
        [Fact]
        public void TestListSortedByName()
        {
            var hostMock = new Mock<EnvironmentVariableCommand.IHost>(MockBehavior.Strict);

            // Returned unsorted - the command is expected to sort by name.
            hostMock.Setup(mock => mock.GetEnvironmentVariables()).Returns(new[]
            {
                ("SHELL", "/bin/sh"),
                ("HOME", "/home/user")
            });

            TestCommandWriteLine(new EnvironmentVariableCommand(hostMock.Object), "env",
                                 "Name  Value",
                                 "----  -----",
                                 "HOME  /home/user",
                                 "SHELL /bin/sh");

            hostMock.Verify();
        }

        [Fact]
        public void TestGetExisting()
        {
            var hostMock = new Mock<EnvironmentVariableCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GetEnvironmentVariable("HOME")).Returns("/home/user");

            TestCommandWriteLine(new EnvironmentVariableCommand(hostMock.Object), "env get HOME",
                                 "/home/user");

            hostMock.Verify();
        }

        [Fact]
        public void TestGetNonExistingWritesNothing()
        {
            var hostMock = new Mock<EnvironmentVariableCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GetEnvironmentVariable("MISSING")).Returns((string)null!);

            TestCommand(new EnvironmentVariableCommand(hostMock.Object), "env get MISSING");

            hostMock.Verify();
        }

        [Fact]
        public void TestSet()
        {
            var hostMock = new Mock<EnvironmentVariableCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.SetEnvironmentVariable("HOME", "myhome"));

            TestCommand(new EnvironmentVariableCommand(hostMock.Object), "env set HOME myhome");

            hostMock.Verify();
        }

        [Fact]
        public void TestDelete()
        {
            var hostMock = new Mock<EnvironmentVariableCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.SetEnvironmentVariable("HOME", null));

            TestCommand(new EnvironmentVariableCommand(hostMock.Object), "env delete HOME");

            hostMock.Verify();
        }
    }
}
