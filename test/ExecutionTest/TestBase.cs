using ExecutionTest.Commands;
using SoftShell;
using SoftShell.Console;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ExecutionTest
{
    public abstract class TestBase
    {
        protected SoftShellHost Shell { get; } = new SoftShellHost(userAuthentication: null, wantsInfoAtSessionStart: false);

        protected TestTerminalInterface Terminal { get; } = new TestTerminalInterface();

        protected ConcurrentDictionary<int, Type> ExecutedCommandTypes { get; } = new ConcurrentDictionary<int, Type>();
        protected System.Exception? Exception { get; set; } = null;

        protected List<TestCommand> TestCommands { get; } = new List<TestCommand>();

        public TestBase()
        {
            // Register test commands
            TestCommands.Add(new EchoCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));
            TestCommands.Add(new ParmsAndOptionsCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));
            TestCommands.Add(new PassThroughCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));
            TestCommands.Add(new ReverseCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));
            TestCommands.Add(new SubcmdAndNonSubcmdCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));
            TestCommands.Add(new SubcmdOnlyCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));
            TestCommands.Add(new ThrowExceptionCommand((i, t) => ExecutedCommandTypes[i] = t, ex => Exception = ex));

            foreach (var command in TestCommands)
                Shell.AddCommand("test", "Test", command);

            // Connect the test terminal interface to the shell
            Shell.AddTerminalListener(new TestTerminalListener(Terminal));
        }

        protected void TestCommandLineOk(string line, params Type[] expectedCommandTypes)
            => TestCommandLineOk(line, null, expectedCommandTypes);

        protected void TestCommandLineOk(string line, string? expectedTextOutput, params Type[] expectedCommandTypes)
        {
            // Prepare
            ExecutedCommandTypes.Clear();
            Exception = null;
            Terminal.FlushInputAsync(CancellationToken.None).Wait();
            Terminal.ClearWrittenText();
            Terminal.IsCommandChainStarted = false;
            Terminal.IsCommandChainEnded = false;

            // Inject the command line through the test terminal interface
            Terminal.ReceiveLine(line);

            // Await command(s) to complete
            while (!Terminal.IsCommandChainEnded)
                Thread.Sleep(10);

            // Verify text output?
            if (expectedTextOutput != null)
                Assert.Equal(expectedTextOutput.Trim(), Terminal.WrittenText?.Trim());

            // Verify that expected command(s) have run
            Assert.Equal(expectedCommandTypes.Length, ExecutedCommandTypes.Count);
            for (int i = 0; i < expectedCommandTypes.Length; i++)
            {
                Assert.True(ExecutedCommandTypes.TryGetValue(i, out var commandType));
                Assert.Equal(expectedCommandTypes[i], commandType);
            }

            // Verify no exceptions are thrown
            Assert.Null(Exception);
        }

        protected void TestCommandLineException(string line, params Type[] expectedCommandTypes)
            => TestCommandLineException(line, null, expectedCommandTypes);

        protected void TestCommandLineException(string line, string? expectedTextOutput, params Type[] expectedCommandTypes)
        {
            // Prepare
            ExecutedCommandTypes.Clear();
            Exception = null;
            Terminal.FlushInputAsync(CancellationToken.None).Wait();
            Terminal.ClearWrittenText();
            Terminal.IsCommandChainStarted = false;
            Terminal.IsCommandChainEnded = false;

            // Inject the command line through the test terminal interface
            Terminal.ReceiveLine(line);

            // Await command(s) to complete
            while (!Terminal.IsCommandChainEnded)
                Thread.Sleep(10);

            // Allow async terminal output to be handled before checking
            // TODO: Can we avoid this?
            Thread.Sleep(500);

            // Verify text output?
            if (expectedTextOutput != null)
                Assert.Equal($"(TextColor:Red){expectedTextOutput.Trim()}(TextColor:Default)", Terminal.WrittenText?.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim());

            // Verify that expected command(s) have run
            Assert.Equal(expectedCommandTypes.Length, ExecutedCommandTypes.Count);
            for (int i = 0; i < expectedCommandTypes.Length; i++)
            {
                Assert.True(ExecutedCommandTypes.TryGetValue(i, out var commandType));
                Assert.Equal(expectedCommandTypes[i], commandType);
            }
        }
    }
}
