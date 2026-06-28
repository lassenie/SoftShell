// AssemblyName.VersionCompatibility / ProcessorArchitecture / HashAlgorithm / CodeBase are obsolete on
// net8.0 but not on the netstandard2.0 target that the command is compiled against, where it prints them.
#pragma warning disable SYSLIB0037
#pragma warning disable SYSLIB0044

using Moq;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class AssemblyCommandTest : TestBase
    {
        private static AssemblyName MakeName(string name, string version)
            => new AssemblyName(name) { Version = new Version(version), CultureInfo = CultureInfo.InvariantCulture };

        [Fact]
        public void TestList()
        {
            var entry = new AssemblyCommand.AssemblyObj(new object());
            var other = new AssemblyCommand.AssemblyObj(new object());

            var hostMock = new Mock<AssemblyCommand.IHost>(MockBehavior.Strict);
            hostMock.Setup(m => m.GetEntryAssembly()).Returns(entry);
            hostMock.Setup(m => m.GetOtherAssembliesInNameOrder(entry)).Returns(new[] { other });
            hostMock.Setup(m => m.GetAssemblyName(entry)).Returns(MakeName("MyApp", "1.0.0.0"));
            hostMock.Setup(m => m.GetAssemblyName(other)).Returns(MakeName("SoftShell", "2.3.4.5"));

            var lines = RunAndCaptureLines(new AssemblyCommand(hostMock.Object), "asm");

            Assert.Equal(new[]
            {
                "Name      Version Culture",
                "----      ------- -------",
                "MyApp     1.0.0.0 ",
                "SoftShell 2.3.4.5 "
            }, lines);
        }

        [Fact]
        public void TestDetails()
        {
            var entry = new AssemblyCommand.AssemblyObj(new object());
            var other = new AssemblyCommand.AssemblyObj(new object());

            var entryName = MakeName("MyLib", "1.2.3.4");
            var otherName = MakeName("SoftShell", "2.3.4.5");

            var hostMock = new Mock<AssemblyCommand.IHost>(MockBehavior.Strict);
            hostMock.Setup(m => m.GetEntryAssembly()).Returns(entry);
            hostMock.Setup(m => m.GetOtherAssembliesInNameOrder(entry)).Returns(new[] { other });
            hostMock.Setup(m => m.GetAssemblyName(entry)).Returns(entryName);
            hostMock.Setup(m => m.GetAssemblyName(other)).Returns(otherName);

            var lines = RunAndCaptureLines(new AssemblyCommand(hostMock.Object), "asm details MyLib");

            Assert.Equal(new[]
            {
                $"Name:                  {entryName.Name}",
                $"Full name:             {entryName.FullName}",
                $"Version:               {entryName.Version}",
                $"Version compatibility: {entryName.VersionCompatibility}",
                $"Culture:               {entryName.CultureName}",
                $"Code base:             {entryName.CodeBase}",
                $"Content type:          {entryName.ContentType}",
                $"CPU architecture:      {entryName.ProcessorArchitecture}",
                $"Flags:                 {entryName.Flags}",
                $"Hash algorithm:        {entryName.HashAlgorithm}"
            }, lines);
        }
    }
}
