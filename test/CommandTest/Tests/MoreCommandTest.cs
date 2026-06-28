using SoftShell.Commands;
using SoftShell.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class MoreCommandTest : TestBase
    {
        [Fact]
        public void TestWithoutFileRequiresPipedInput()
        {
            // No file argument and input that is not piped -> the command should reject it.
            // The command reads the window size before checking the input, hence the two output calls.
            TestCommandThrows(new MoreCommand(), "more",
                              "Without a file argument a piped input is expected for this command.",
                              new[] { (nameof(ICommandInput.IsPiped), new object[0], (object?)false) },
                              new[]
                              {
                                  (nameof(ICommandOutput.WindowWidth), new object[0], (object?)80),
                                  (nameof(ICommandOutput.WindowHeight), new object[0], (object?)25)
                              });
        }

        [Fact]
        public void TestNonExistingFile()
        {
            var missingFile = "this_file_does_not_exist_3f8c1.txt";

            TestCommandThrows(new MoreCommand(), $"more {missingFile}",
                              $"File {missingFile} not found.",
                              NoInput(),
                              new[]
                              {
                                  (nameof(ICommandOutput.WindowWidth), new object[0], (object?)80),
                                  (nameof(ICommandOutput.WindowHeight), new object[0], (object?)25)
                              });
        }
    }
}
