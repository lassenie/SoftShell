using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class GrepCommandTest : TestBase
    {
        [Fact]
        public void TestSingleExpression()
        {
            TestCommandTextReadWriteLine(new GrepCommand(), "grep abc.*",
                new[] { "ab", "abc", "Abc", "abcd", "Abcd" },
                new[] { "abc", "abcd" });
        }

        [Fact]
        public void TestMultipleExpressions()
        {
            TestCommandTextReadWriteLine(new GrepCommand(), "grep \"abc.*\\|^def$\"",
                new[] { "ab", "abcd", "cdefg", "def" },
                new[] { "abcd", "def" });
        }

        [Fact]
        public void TestIgnoreCase()
        {
            TestCommandTextReadWriteLine(new GrepCommand(), "grep abc.* -ignorecase",
                new[] { "ab", "abc", "Abc", "abcd", "Abcd" },
                new[] { "abc", "Abc", "abcd", "Abcd" });
        }

        [Fact]
        public void TestInvertMatch()
        {
            TestCommandTextReadWriteLine(new GrepCommand(), "grep abc.* -invertmatch",
                new[] { "ab", "abc", "Abc", "abcd", "Abcd" },
                new[] { "ab", "Abc", "Abcd" });
        }
    }
}
