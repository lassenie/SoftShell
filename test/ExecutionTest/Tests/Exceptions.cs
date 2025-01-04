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
                                     "Throw: Hello",
                                     typeof(ThrowExceptionCommand));
        }

        [Fact]
        public void TestExceptionThrownAndPassedThrough()
        {
            TestCommandLineException("throw Hello|passthrough",
                                     "Throw: HelloPassThrough: Command cancelled.",
                                     typeof(ThrowExceptionCommand),
                                     typeof(PassThroughCommand));

            TestCommandLineException("throw Hello|reverse|passthrough",
                                     "Throw: HelloReverse: Command cancelled.PassThrough: Command cancelled.",
                                     typeof(ThrowExceptionCommand),
                                     typeof(ReverseCommand),
                                     typeof(PassThroughCommand));
        }
    }
}
