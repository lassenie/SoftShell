using Moq;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class ProcessCommandTest : TestBase
    {
        [Fact]
        public void TestInfo()
        {
            var process = new ProcessCommand.ProcessObj(new object());

            var hostMock = new Mock<ProcessCommand.IHost>(MockBehavior.Strict);
            hostMock.Setup(m => m.GetCurrentProcess()).Returns(process);
            hostMock.Setup(m => m.GetId(process)).Returns(1234);
            hostMock.Setup(m => m.GetSessionId(process)).Returns(1);
            hostMock.Setup(m => m.GetProcessName(process)).Returns("myapp");
            hostMock.Setup(m => m.GetMainWindowTitle(process)).Returns(string.Empty);
            hostMock.Setup(m => m.GetMainModuleFileName(process)).Returns(@"C:\app\myapp.exe");
            hostMock.Setup(m => m.GetCommandLineArgs()).Returns(new[] { "host.dll", "--flag", "value" });
            hostMock.Setup(m => m.GetCurrentDirectory()).Returns(@"C:\work");
            hostMock.Setup(m => m.GetPriorityClass(process)).Returns("Normal");
            hostMock.Setup(m => m.GetBasePriority(process)).Returns(8);
            hostMock.Setup(m => m.GetWorkingSet64(process)).Returns(104857600); // exactly 100 MB
            hostMock.Setup(m => m.GetHandleCount(process)).Returns(42);

            var lines = RunAndCaptureLines(new ProcessCommand(hostMock.Object), "process");

            // The memory value is formatted using the current culture (decimal separator differs per locale).
            var memory = (104857600d / 1048576d).ToString("F2");

            Assert.Equal(new[]
            {
                "Id              :  1234",
                "Session         :  1",
                "Name            :  myapp",
                "Main window     :  ",
                @"File            :  C:\app\myapp.exe",
                "Arguments       :  --flag",
                "                :  value",
                @"Current dir     :  C:\work",
                "Priority class  :  Normal",
                "Base priority   :  8",
                $"Memory, physical:  {memory} MB",
                "Handles         :  42"
            }, lines);

            hostMock.Verify();
        }

        [Fact]
        public void TestKill()
        {
            var process = new ProcessCommand.ProcessObj(new object());

            var hostMock = new Mock<ProcessCommand.IHost>(MockBehavior.Strict);
            hostMock.Setup(m => m.GetCurrentProcess()).Returns(process);
            hostMock.Setup(m => m.Kill(process));

            TestCommandWriteLine(new ProcessCommand(hostMock.Object), "process kill",
                                 "Killing the process...");

            hostMock.Verify();
        }
    }
}
