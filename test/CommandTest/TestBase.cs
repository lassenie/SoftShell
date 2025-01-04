using Microsoft.VisualStudio.TestPlatform.Utilities;

using Moq;
using SoftShell;
using SoftShell.Exceptions;
using SoftShell.Execution;
using SoftShell.IO;
using SoftShell.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest
{
    public partial class TestBase
    {
        protected void TestCommandWriteLine(Command command, string commandLine,
                         params string[] writtenLines)
            => TestCommand(command, commandLine,
                           NoInput(),
                           writtenLines
                           .Select(ln => (method: nameof(ICommandOutput.WriteLineAsync),
                                          args: new object[] { ln },
                                          returnVal: (object?)null))
                           .ToArray(),
                           NoErrorOutput(),
                           NoContextCalls());

        protected void TestCommandTextReadWriteLine(Command command, string commandLine,
                         string[] linesToRead,
                         string[] writtenLines)
            => TestCommand(command, commandLine,
                           linesToRead
                                .SelectMany(ln => new[]
                                {
                                    (method: nameof(ICommandInput.IsEnded), args: new object[0], returnVal: (object?)false),
                                    (method: nameof(ICommandInput.ReadLineAsync), args: new object[0], returnVal: (object?)ln)
                                })
                                .Append((method: nameof(ICommandInput.IsEnded), args: new object[0], returnVal: (object?)true))
                                .ToArray(),
                           writtenLines.Select(ln => (method: nameof(ICommandOutput.WriteLineAsync), args: new object[] { ln }, returnVal: (object?)null)).ToArray(),
                           NoErrorOutput(),
                           NoContextCalls());

        protected void TestCommandClearScreen(Command command, string commandLine)
            => TestCommand(command, commandLine, (method: nameof(ICommandOutput.ClearScreenAsync), args:   new object[0], returnVal: (object?)null));

        protected void TestCommandExit(Command command, string commandLine)
            => TestCommand(command, commandLine,
                           NoInput(),
                           NoOutput(),
                           NoErrorOutput(),
                           new[] { (method: nameof(ICommandExecutionContext.TerminateSessionAsync),
                                    args: new object[0],
                                    returnVal: (object?)null) });

        protected void TestCommand(Command command, string commandLine,
                                   params (string method, object[] args, object? returnVal)[] expectedOutputCalls)
            => TestCommand(command, commandLine,
                           NoInput(),
                           expectedOutputCalls,
                           NoErrorOutput(), NoContextCalls());

        protected void TestCommand(Command command, string commandLine,
                                   (string method, object[] args, object? returnVal)[] expectedInputCalls,
                                   (string method, object[] args, object? returnVal)[] expectedOutputCalls,
                                   (string method, object[] args, object? returnVal)[] expectedErrorOutputCalls,
                                   (string method, object[] args, object? returnVal)[] expectedContextCalls)
        {
            var inputMock = new InputMock(expectedInputCalls);
            var outputMock = new OutputMock(AddCommonOutputCalls(expectedOutputCalls));
            var errorOutputMock = new ErrorOutputMock(expectedErrorOutputCalls);
            var exceptionHandlerMock = new Mock<ITestExceptionHandler>(MockBehavior.Strict);

            List<(string method, object[] args)> contextCalls = new();

            ExecuteCommandAsync(command, commandLine, inputMock, outputMock, errorOutputMock, exceptionHandlerMock.Object, contextCalls).Wait();

            inputMock.Verify();
            outputMock.Verify();
            exceptionHandlerMock.Verify();

            Assert.Equal(expectedContextCalls.Length, contextCalls.Count);

            for (int i = 0; i < contextCalls.Count; i++)
            {
                Assert.Equal(expectedContextCalls[i].method, contextCalls[i].method);
                Assert.Equal(expectedContextCalls[i].args.Length, contextCalls[i].args.Length);

                for (int a = 0; a < contextCalls[i].args.Length; a++)
                    Assert.Equal(expectedContextCalls[i].args[a], contextCalls[i].args[a]);
            }
        }

        private IEnumerable<(string method, object[] args, object? returnVal)> AddCommonOutputCalls((string method, object[] args, object? returnVal)[] expectedOutputCalls)
        {
            yield return (nameof(ICommandOutput.WindowWidth), new object[0], 80);
            yield return (nameof(ICommandOutput.WindowHeight), new object[0], 25);

            foreach (var expectedOutputCall in expectedOutputCalls)
                yield return expectedOutputCall;

            yield return (nameof(ICommandOutput.CommandOutputEndAsync), new object[0], null);

        }

        private Task ExecuteCommandAsync(Command command, string commandLine,
                                         ICommandInput input, ICommandOutput output, ICommandErrorOutput errorOutput,
                                         ITestExceptionHandler exceptionHandler,
                                         List<(string method, object[] args)> contextCalls)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (commandLine == null) throw new ArgumentNullException(nameof(commandLine));

            var parser = new CommandLineTokenizer();
            (var commandTokens, var _) = parser.Tokenize(commandLine);

            if (commandTokens.Count() == 0) throw new ArgumentException("Command line not containing any valid tokens.", nameof(commandLine));
            if (commandTokens.Count() > 1) throw new ArgumentException("Command line must only invoke one command.", nameof(commandLine));

            var commandInvocation = new CommandInvokation(command, commandTokens.First().text, 1, commandTokens.First().tokens);

            var context = new ConcreteCommandExecutionContext(new TestSession(), errorOutput, commandInvocation, 0, output.WindowWidth, output.WindowHeight, new CancellationTokenSource(), () => Task.CompletedTask);

            context.Input = input;
            context.Output = output;
            context.ExceptionOutput = new TestErrorOutput(exceptionHandler);
            context.LogCall = (method, args) => contextCalls.Add((method, args));

            return command.RunAsync(context, commandTokens.First().text, commandTokens.First().tokens);
        }

        protected (string method, object[] args, object? returnVal)[] NoInput() => new (string method, object[] args, object? returnVal)[0];
        protected (string method, object[] args, object? returnVal)[] NoOutput() => new (string method, object[] args, object? returnVal)[0];
        protected (string method, object[] args, object? returnVal)[] NoErrorOutput() => new (string method, object[] args, object? returnVal)[0];
        protected (string method, object[] args, object? returnVal)[] NoContextCalls() => new (string method, object[] args, object? returnVal)[0];
    }
}
