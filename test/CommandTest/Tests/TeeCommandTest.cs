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
        // The command derives the directory to create via Path.GetDirectoryName, which is
        // platform-specific. Build the path with the running platform's separator so the test
        // works on both Windows ("a\b.txt") and Unix ("a/b.txt").
        private static readonly string Directory = "a";
        private static readonly string FilePath = Path.Combine(Directory, "b.txt");

        [Fact]
        void TestEmptyFile()
        {
            TestTeeCommand($"tee {FilePath}", Directory, FilePath, false, new byte[0]);
            TestTeeCommand($"tee {FilePath} -append", Directory, FilePath, true, new byte[0]);
        }

        [Fact]
        void TestShortFile()
        {
            TestTeeCommand($"tee {FilePath}", Directory, FilePath, false, new byte[] { (byte)'x' }, "x");
            TestTeeCommand($"tee -append {FilePath}", Directory, FilePath, true, new byte[] { (byte)'x' }, "x");
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

            TestTeeCommand($"tee {FilePath}", Directory, FilePath, false, expectedFileOutput, input);
            TestTeeCommand($"tee {FilePath} -append", Directory, FilePath, true, expectedFileOutput, input);
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
