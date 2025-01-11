using ExecutionTest.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest.Tests
{
    public class Exceptions : TestBase
    {
        [Fact]
        public void TestExceptionThrown()
        {
            TestCommandLineException("throw Hello",
                                     "throw: Hello",
                                     typeof(ThrowExceptionCommand));
        }

        [Fact]
        public void TestExceptionThrownAndPassedThrough()
        {
            TestCommandLineException("throw Hello|passthrough",
                                     "throw: Hellopassthrough: Command cancelled.",
                                     typeof(ThrowExceptionCommand),
                                     typeof(PassThroughCommand));

            TestCommandLineException("throw Hello|reverse|passthrough",
                                     "throw: Helloreverse: Command cancelled.passthrough: Command cancelled.",
                                     typeof(ThrowExceptionCommand),
                                     typeof(ReverseCommand),
                                     typeof(PassThroughCommand));
        }
    }
}
