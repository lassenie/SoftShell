using SoftShell.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CommandTest
{
    public partial class TestBase
    {
        private abstract class MockBase
        {
            protected Queue<(string method, object[] args, object? returnVal)> _expectedCalls;

            public MockBase(IEnumerable<(string method, object[] args, object? returnVal)> expectedCalls)
            {
                _expectedCalls = new Queue<(string method, object[] args, object? returnVal)>(expectedCalls);
            }

            public void Verify() => Assert.False(_expectedCalls.TryPeek(out _));

        }

        private class InputMock : MockBase, ICommandInput
        {
            public InputMock(IEnumerable<(string method, object[] args, object? returnVal)> expectedCalls) : base(expectedCalls)
            {
            }

            public bool IsPiped
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(IsPiped));
                    Assert.Empty(call.args);
                    return (bool)(call.returnVal ?? false);
                }
            }

            public bool IsEnded
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(IsEnded));
                    Assert.Empty(call.args);
                    return (bool)(call.returnVal ?? false);
                }
            }

            public Task FlushInputAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(FlushInputAsync), call.method);
                Assert.Empty(call.args);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task<string> ReadAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(ReadAsync), call.method);
                Assert.Empty(call.args);
                return Task.FromResult(call.returnVal?.ToString() ?? string.Empty);
            }

            public Task<string> ReadLineAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(ReadLineAsync), call.method);
                Assert.Empty(call.args);
                return Task.FromResult(call.returnVal?.ToString() ?? string.Empty);
            }

            public Task<string> TryReadAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(TryReadAsync), call.method);
                Assert.Empty(call.args);
                return Task.FromResult(call.returnVal?.ToString() ?? string.Empty);
            }
        }

        private class OutputMock : MockBase, ICommandOutput
        {
            public OutputMock(IEnumerable<(string method, object[] args, object? returnVal)> expectedCalls) : base(expectedCalls)
            {
            }

            public bool IsPiped
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(IsPiped));
                    Assert.Empty(call.args);
                    return (bool)(call.returnVal ?? false);
                }
            }

            public int? WindowWidth
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(WindowWidth));
                    Assert.Empty(call.args);
                    return (int?)call.returnVal;
                }
            }

            public int? WindowHeight
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(WindowHeight));
                    Assert.Empty(call.args);
                    return (int?) call.returnVal;
                }
            }

            public string LineTermination
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(LineTermination));
                    Assert.Empty(call.args);
                    return call.returnVal?.ToString() ?? string.Empty;
                }
            }

            public Task ClearScreenAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(ClearScreenAsync), call.method);
                Assert.Empty(call.args);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task CommandOutputEndAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(CommandOutputEndAsync), call.method);
                Assert.Empty(call.args);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task WriteAsync(string text)
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(WriteAsync), call.method);
                Assert.Single(call.args);
                Assert.Equal(call.args[0], text);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task WriteLineAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(WriteLineAsync), call.method);
                Assert.Empty(call.args);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task WriteLineAsync(string text)
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(WriteLineAsync), call.method);
                Assert.Single(call.args);
                Assert.Equal(call.args[0], text);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }
        }

        private class ErrorOutputMock : MockBase, ICommandErrorOutput
        {
            public ErrorOutputMock(IEnumerable<(string method, object[] args, object? returnVal)> expectedCalls) : base(expectedCalls)
            {
                _expectedCalls.Enqueue((nameof(CommandErrorOutputEndAsync), new object[0], null));
            }

            public int? WindowWidth
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(WindowWidth));
                    Assert.Empty(call.args);
                    return (int?)call.returnVal;
                }
            }

            public int? WindowHeight
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(WindowHeight));
                    Assert.Empty(call.args);
                    return (int?)call.returnVal;
                }
            }

            public string LineTermination
            {
                get
                {
                    var call = _expectedCalls.Dequeue();

                    Assert.Equal(call.method, nameof(LineTermination));
                    Assert.Empty(call.args);
                    return call.returnVal?.ToString() ?? string.Empty;
                }
            }

            public Task CommandErrorOutputEndAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(CommandErrorOutputEndAsync), call.method);
                Assert.Empty(call.args);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task WriteAsync(string text)
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(WriteAsync), call.method);
                Assert.Single(call.args);
                Assert.Equal(call.args[0], text);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task WriteLineAsync()
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(WriteLineAsync), call.method);
                Assert.Empty(call.args);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }

            public Task WriteLineAsync(string text)
            {
                var call = _expectedCalls.Dequeue();

                Assert.Equal(nameof(WriteLineAsync), call.method);
                Assert.Single(call.args);
                Assert.Equal(call.args[0], text);
                Assert.Null(call.returnVal);
                return Task.CompletedTask;
            }
        }
    }
}
