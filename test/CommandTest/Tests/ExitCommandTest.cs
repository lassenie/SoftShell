using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class ExitCommandTest : TestBase
    {
        [Fact]
        public void Test()
        {
            TestCommandExit(new ExitCommand(), "exit");

        }
    }
}
