using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class ClearScreenCommandTest : TestBase
    {
        [Fact]
        public void Test()
        {
            TestCommandClearScreen(new ClearScreenCommand(),
                                   "cls");
        }
    }
}
