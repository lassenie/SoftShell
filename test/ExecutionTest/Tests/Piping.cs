using ExecutionTest.Commands;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Tests
{
    public class Piping : TestBase
    {
        [Fact]
        public void TestPiping1()
        {
            TestCommandLineOk("echo \"Just a test\"|reverse",
                              "\n\rtset a tsuJ",
                              typeof(EchoCommand),
                              typeof(ReverseCommand));
        }

        [Fact]
        public void TestPiping2()
        {
            TestCommandLineOk("echo \"Just a test\"|reverse|passthrough|reverse",
                              "Just a test\r\n",
                              typeof(EchoCommand),
                              typeof(ReverseCommand),
                              typeof(PassThroughCommand),
                              typeof(ReverseCommand));
        }
    }
}
