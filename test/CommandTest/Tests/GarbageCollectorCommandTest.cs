using Moq;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class GarbageCollectorCommandTest : TestBase
    {
        [Fact]
        public void TestCollect()
        {
            var hostMock = new Mock<GarbageCollectorCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GC_Collect());

            TestCommand(new GarbageCollectorCommand(hostMock.Object), "gc collect",
                        ("WriteLineAsync", new object[] { "Collecting garbage..." }, (object?)null),
                        ("WriteLineAsync", new object[] { "Garbage collected." }, (object?)null));

            hostMock.Verify();
        }

        [Fact]
        public void TestDisable1000()
        {
            var hostMock = new Mock<GarbageCollectorCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GC_StartNoGCRegion(1000)).Returns(() => true);

            TestCommandWriteLine(new GarbageCollectorCommand(hostMock.Object), "gc disable -totalsize=1000",
                                 "Disabling garbage collection...",
                                 "Now disabled until enabled by the 'gc enable' command or the host program itself, or until the given max. number of allocated bytes are exceeded.");

            hostMock.Verify();
        }

        [Fact]
        public void TestDisable1K()
        {
            var hostMock = new Mock<GarbageCollectorCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GC_StartNoGCRegion(1024)).Returns(() => true);

            TestCommandWriteLine(new GarbageCollectorCommand(hostMock.Object), "gc disable -totalsize=1k",
                                 "Disabling garbage collection...",
                                 "Now disabled until enabled by the 'gc enable' command or the host program itself, or until the given max. number of allocated bytes are exceeded.");

            hostMock.Verify();
        }

        [Fact]
        public void TestDisable1M()
        {
            var hostMock = new Mock<GarbageCollectorCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GC_StartNoGCRegion(1048576)).Returns(() => true);

            TestCommandWriteLine(new GarbageCollectorCommand(hostMock.Object), "gc disable -totalsize=1m",
                                 "Disabling garbage collection...",
                                 "Now disabled until enabled by the 'gc enable' command or the host program itself, or until the given max. number of allocated bytes are exceeded.");

            hostMock.Verify();
        }

        [Fact]
        public void TestEnable()
        {
            var hostMock = new Mock<GarbageCollectorCommand.IHost>(MockBehavior.Strict);

            hostMock.Setup(mock => mock.GC_EndNoGCRegion()).Returns(() => true);

            TestCommand(new GarbageCollectorCommand(hostMock.Object), "gc enable",
                        ("WriteLineAsync", new object[] { "Enabling garbage collection..." }, (object?)null),
                        ("WriteLineAsync", new object[] { "Garbage collection enabled." }, (object?)null));

            hostMock.Verify();
        }

    }
}
