using Moq;
using SoftShell;
using SoftShell.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest
{
    public partial class TestBase
    {
        /// <summary>
        /// Runs a command and captures its standard output as a list of written lines.
        /// Useful for commands whose output is formatted into aligned columns, where asserting
        /// against a readable list of lines is clearer than a strict call sequence.
        /// Calls to <see cref="ICommandOutput.WriteAsync(string)"/> (partial lines) are appended
        /// to the current line being built.
        /// </summary>
        protected List<string> RunAndCaptureLines(Command command, string commandLine,
                                                  (string method, object[] args, object? returnVal)[]? expectedInputCalls = null)
        {
            var lines = new List<string>();
            var current = new StringBuilder();

            void FlushLine()
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            var inputMock = new InputMock(expectedInputCalls ?? NoInput());

            var outputMock = new Mock<ICommandOutput>(MockBehavior.Loose);
            outputMock.SetupGet(o => o.WindowWidth).Returns(80);
            outputMock.SetupGet(o => o.WindowHeight).Returns(25);
            outputMock.SetupGet(o => o.IsPiped).Returns(false);
            outputMock.SetupGet(o => o.LineTermination).Returns("\r\n");
            outputMock.Setup(o => o.WriteAsync(It.IsAny<string>()))
                      .Returns(Task.CompletedTask)
                      .Callback<string>(text => current.Append(text));
            outputMock.Setup(o => o.WriteLineAsync())
                      .Returns(Task.CompletedTask)
                      .Callback(FlushLine);
            outputMock.Setup(o => o.WriteLineAsync(It.IsAny<string>()))
                      .Returns(Task.CompletedTask)
                      .Callback<string>(text => { current.Append(text); FlushLine(); });
            outputMock.Setup(o => o.ClearScreenAsync()).Returns(Task.CompletedTask);
            outputMock.Setup(o => o.CommandOutputEndAsync()).Returns(Task.CompletedTask);

            var errorOutputMock = new Mock<ICommandErrorOutput>(MockBehavior.Loose);
            errorOutputMock.Setup(o => o.CommandErrorOutputEndAsync()).Returns(Task.CompletedTask);

            var exceptionHandlerMock = new Mock<ITestExceptionHandler>(MockBehavior.Loose);

            ExecuteCommandAsync(command, commandLine, inputMock, outputMock.Object, errorOutputMock.Object,
                                exceptionHandlerMock.Object, new List<(string method, object[] args)>()).Wait();

            // Include any trailing text not terminated by a line break.
            if (current.Length > 0)
                FlushLine();

            inputMock.Verify();

            return lines;
        }
    }
}
