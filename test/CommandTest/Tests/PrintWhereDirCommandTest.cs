using Moq;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class PrintWhereDirCommandTest : TestBase
    {
        [Fact]
        public void TestPrintsCurrentDirectory()
        {
            var hostMock = new Mock<PrintWhereDirCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GetCurrentDirectory()).Returns(@"C:\some\dir");

            TestCommandWriteLine(new PrintWhereDirCommand(hostMock.Object), "pwd",
                                 @"C:\some\dir");

            hostMock.Verify();
        }
    }
}
