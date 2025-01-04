using Moq;
using SoftShell.Commands;
using SoftShell.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class TeeCommandTest : TestBase
    {
        [Fact]
        void TestEmptyFile()
        {
            TestTeeCommand("tee a\\b.txt", "a", "a\\b.txt", false, new byte[0]);
            TestTeeCommand("tee a\\b.txt -append", "a", "a\\b.txt", true, new byte[0]);
        }

        [Fact]
        void TestShortFile()
        {
            TestTeeCommand("tee a\\b.txt", "a", "a\\b.txt", false, new byte[] { (byte)'x' }, "x");
            TestTeeCommand("tee -append a\\b.txt", "a", "a\\b.txt", true, new byte[] { (byte)'x' }, "x");
        }

        [Fact]
        void TestLongerFile()
        {
            string[] input = new[]
            {
                "AZaz09Æø \t\n",
                " \r\n[]%"
            };
            byte[] expectedFileOutput = Encoding.UTF8.GetBytes("AZaz09Æø \t\n \r\n[]%");

            TestTeeCommand("tee a\\b.txt", "a", "a\\b.txt", false, expectedFileOutput, input);
            TestTeeCommand("tee a\\b.txt -append", "a", "a\\b.txt", true, expectedFileOutput, input);
        }

        void TestTeeCommand(string commandLine, string directory, string filePath, bool append, byte[] expectedOutput, params string[] textInput)
        {
            MemoryStream fileOutput = new();

            var mock = new Mock<TeeCommand.IHost>(MockBehavior.Strict);

            mock.Setup(x => x.CreateDirectory(directory));
            mock.Setup(x => x.BeginFileWrite(filePath, append, Encoding.UTF8, false)).Returns(new StreamWriter(fileOutput));

            List<(string method, object[] args, object? returnVal)> input = new();
            List<(string method, object[] args, object? returnVal)> output = new();

            foreach (var inputText in textInput)
            {
                input.Add((nameof(ICommandInput.IsEnded), new object[0], (object?)false));
                input.Add((nameof(ICommandInput.ReadAsync), new object[0], inputText));
                output.Add((nameof(ICommandOutput.WriteAsync), new object[] { inputText }, (object?)null));
            }
            input.Add((nameof(ICommandInput.IsEnded), new object[0], (object?)true));

            TestCommand(new TeeCommand(mock.Object), commandLine,
                input.ToArray(),
                output.ToArray(),
                NoErrorOutput(),
                NoContextCalls());

            mock.Verify();
            Assert.Equal(expectedOutput, fileOutput.ToArray());
        }
    }
}
