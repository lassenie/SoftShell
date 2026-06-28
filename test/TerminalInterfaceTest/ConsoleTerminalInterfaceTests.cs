using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell;

namespace TerminalInterfaceTest
{
    /// <summary>
    /// Tests for the console terminal interface (<c>SoftShell.Console.ConsoleTerminalInterface</c>).
    ///
    /// The console interface is a thin wrapper over <see cref="System.Console"/>. Its input side
    /// (<c>TryReadAsync</c>/<c>ReadLineAsync</c>) relies on <c>Console.KeyAvailable</c>/<c>ReadKey</c>,
    /// which are not available when the test runner has redirected the standard input, so only the
    /// output side and the static properties are exercised here. To capture output the standard
    /// output is redirected to a <see cref="StringWriter"/> for the duration of each test.
    /// </summary>
    public class ConsoleTerminalInterfaceTests
    {
        /// <summary>
        /// Creates a console terminal interface, redirects <see cref="System.Console"/> output to a
        /// <see cref="StringWriter"/>, runs <paramref name="action"/> and returns whatever was written.
        /// The original output writer is always restored.
        /// </summary>
        private static async Task<string> CaptureOutputAsync(Func<TerminalInterface, Task> action)
        {
            var originalOut = System.Console.Out;

            // Construct first: the constructor sets the console encoding, which itself replaces
            // Console.Out, so the capture writer must be installed afterwards.
            var terminal = (TerminalInterface)new SoftShell.Console.ConsoleTerminalInterface();
            var writer = new StringWriter();

            try
            {
                System.Console.SetOut(writer);
                await action(terminal);
            }
            finally
            {
                System.Console.SetOut(originalOut);
                terminal.Dispose();
            }

            return writer.ToString();
        }

        [Fact]
        public void Properties_DescribeTheLocalConsole()
        {
            var originalOut = System.Console.Out;
            var terminal = (TerminalInterface)new SoftShell.Console.ConsoleTerminalInterface();

            try
            {
                Assert.Equal("Console", terminal.TerminalType);
                Assert.Equal("Local", terminal.TerminalInstanceInfo);
                Assert.Equal(Environment.NewLine, terminal.LineTermination);
                Assert.Equal(Encoding.UTF8, terminal.Encoding);
            }
            finally
            {
                System.Console.SetOut(originalOut);
                terminal.Dispose();
            }
        }

        [Fact]
        public async Task WriteAsync_WritesTextToTheConsole()
        {
            var output = await CaptureOutputAsync(t => t.WriteAsync("Hello", CancellationToken.None));

            Assert.Equal("Hello", output);
        }

        [Fact]
        public async Task WriteAsync_WithNullText_WritesNothing()
        {
            var output = await CaptureOutputAsync(t => t.WriteAsync(null!, CancellationToken.None));

            Assert.Equal(string.Empty, output);
        }

        [Fact]
        public async Task WriteLineAsync_WritesTextFollowedByNewLine()
        {
            var output = await CaptureOutputAsync(t => t.WriteLineAsync("Hello", CancellationToken.None));

            Assert.Equal("Hello" + Environment.NewLine, output);
        }

        [Fact]
        public async Task SetTextColorAsync_TracksTheCurrentColor()
        {
            var originalOut = System.Console.Out;
            var terminal = (TerminalInterface)new SoftShell.Console.ConsoleTerminalInterface();

            try
            {
                Assert.Null(terminal.CurrentTextColor);

                await terminal.SetTextColorAsync(ConsoleColor.Red, CancellationToken.None);
                Assert.Equal(ConsoleColor.Red, terminal.CurrentTextColor);

                await terminal.SetTextColorAsync(null, CancellationToken.None);
                Assert.Null(terminal.CurrentTextColor);
            }
            finally
            {
                System.Console.SetOut(originalOut);
                terminal.Dispose();
            }
        }

        [Fact]
        public async Task ClearScreenAsync_CompletesWithoutThrowing()
        {
            // With output redirected Console.Clear() cannot actually clear a screen; the interface
            // swallows the resulting error, so the call must still complete successfully.
            var originalOut = System.Console.Out;
            var terminal = (TerminalInterface)new SoftShell.Console.ConsoleTerminalInterface();

            try
            {
                await terminal.ClearScreenAsync(CancellationToken.None);
            }
            finally
            {
                System.Console.SetOut(originalOut);
                terminal.Dispose();
            }
        }
    }
}
