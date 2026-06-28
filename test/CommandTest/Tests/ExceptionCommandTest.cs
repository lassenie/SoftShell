using SoftShell.Commands.Anonymous;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class ExceptionCommandTest : TestBase
    {
        [Fact]
        public void TestThrowsGivenMessage()
        {
            // The (anonymous) command always throws when executed; the message is routed
            // to the error/exception output. The command line itself is ignored.
            TestCommandThrows(new ExceptionCommand("Something went wrong."), "anything",
                              "Something went wrong.");
        }

        [Fact]
        public void TestThrowsGivenException()
        {
            TestCommandThrows(new ExceptionCommand(new InvalidOperationException("Bad state.")), "anything",
                              "Bad state.");
        }
    }
}
