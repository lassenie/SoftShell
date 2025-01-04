using SoftShell;
using SoftShell.Commands;
using System.Diagnostics;

namespace CommandTest.Tests
{
    public class InformationCommandTest : TestBase
    {
        [Fact]
        public void TestNull()
        {
            TestCommandWriteLine(new InformationCommand(null), "info");
        }

        [Fact]
        public void TestNone()
        {
            TestCommandWriteLine(new InformationCommand(() => Enumerable.Empty<string>()), "info");
        }

        [Fact]
        public void TestEmptyLine()
        {
            TestCommandWriteLine(new InformationCommand(() => new[] { string.Empty }),
                                 "info",
                                 "");
        }

        [Fact]
        public void TestOneLine()
        {
            TestCommandWriteLine(new InformationCommand(() => new[] { "Line one" }),
                                 "info",
                                 "Line one");
        }

        [Fact]
        public void TestMultipleLines()
        {
            TestCommandWriteLine(new InformationCommand(() => new[] { "Line one", "Line two" }),
                                 "info",
                                 "Line one",
                                 "Line two");
        }
    }
}