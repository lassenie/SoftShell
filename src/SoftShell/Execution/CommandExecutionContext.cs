using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell.Helpers;
using SoftShell.IO;

namespace SoftShell.Execution
{
    /// <summary>
    /// Context for command execution. Used by <see cref="Command"/>-derived classes for accessing input/output etc.
    /// </summary>
    internal abstract class CommandExecutionContext : ICommandExecutionContext, IKeyboardInput
    {
        private ISession _session;
        private CommandInvokation _commandInvokation;
        private CancellationTokenSource _cancelTokenSource;
        private Func<Task> _updateTerminalInputAsync;

        /// <inheritdoc/>
        public int SessionId => _session.Id;

        /// <inheritdoc/>
        public bool IsExecutionEnded { get; internal set; }

        /// <inheritdoc/>
        public IKeyboardInput Keyboard => this;

        /// <inheritdoc/>
        public ICommandInput Input { get; set; }

        /// <inheritdoc/>
        public ICommandOutput Output { get; set; }

        /// <inheritdoc/>
        public abstract ICommandErrorOutput ErrorOutput { get; }

        /// <inheritdoc/>
        public Command Command => _commandInvokation.Command;

        /// <inheritdoc/>
        public string CommandLine => _commandInvokation.CommandLine;

        /// <inheritdoc/>
        public IEnumerable<CommandLineToken> CommandLineTokens => _commandInvokation.Tokens;

        /// <inheritdoc/>
        public int CommandChainIndex { get; }

        /// <inheritdoc/>
        public CancellationToken CancellationToken => _cancelTokenSource.Token;

        /// <inheritdoc/>
        public bool IsCancellationRequested => _cancelTokenSource.IsCancellationRequested;

        /// <summary>
        /// Delegate that can be set for logging method calls made to this command execution context.
        /// This can be used for unit testing.
        /// Parameters:
        /// * The name of method called.
        /// * The arguments given.
        /// </summary>
        internal Action<string, object[]> LogCall { get; set; } = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The session running the command.</param>
        /// <param name="commandInvokation">Command invocation information.</param>
        /// <param name="commandChainIndex">Index in the command chain (0 is first command).</param>
        /// <param name="cancelTokenSource">Cancellation token source that can be used for cancelling the command.</param>
        /// <param name="updateTerminalInputAsync">Delegate that can be used to asynchonously read buffered input from the terminal.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public CommandExecutionContext(ISession session, CommandInvokation commandInvokation, int commandChainIndex, CancellationTokenSource cancelTokenSource, Func<Task> updateTerminalInputAsync)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _commandInvokation = commandInvokation ?? throw new ArgumentNullException(nameof(commandInvokation));
            _cancelTokenSource = cancelTokenSource;
            _updateTerminalInputAsync = updateTerminalInputAsync ?? throw new ArgumentNullException(nameof(updateTerminalInputAsync)); ;
            CommandChainIndex = commandChainIndex;
        }

        /// <inheritdoc/>
        public void RequestCancel()
        {
            LogCall?.Invoke(nameof(RequestCancel), new object[0]);
            _cancelTokenSource.Cancel();
        }

        /// <inheritdoc/>
        public async Task CancelCommandAsync()
        {
            LogCall?.Invoke(nameof(CancelCommandAsync), new object[0]);

            _cancelTokenSource?.Cancel();

            while (!IsExecutionEnded)
                await Task.Delay(200).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task TerminateSessionAsync(bool hard)
        {
            LogCall?.Invoke(nameof(TerminateSessionAsync), new object[0]);

            _session.Host.TerminateSession(_session.Id, hard);
            return Task.CompletedTask;
        }

        #region IKeyboardInput

        /// <inheritdoc/>
        bool IKeyboardInput.IsAvailable => true;

        /// <inheritdoc/>
        bool IKeyboardInput.IsEnded => _cancelTokenSource.IsCancellationRequested;

        /// <inheritdoc/>
        async Task IKeyboardInput.FlushInputAsync()
        {
            await _updateTerminalInputAsync().ConfigureAwait(false);

            while (_commandInvokation.KeyboardInputBuffer.Any() && !_cancelTokenSource.IsCancellationRequested)
                _commandInvokation.KeyboardInputBuffer.TryDequeue(out var _);
        }

        /// <inheritdoc/>
        async Task<IEnumerable<(KeyAction action, char character)>> IKeyboardInput.ReadAsync(bool echo)
        {
            while (!_cancelTokenSource.IsCancellationRequested)
            {
                await _updateTerminalInputAsync().ConfigureAwait(false);

                while (_commandInvokation.KeyboardInputBuffer.Any() && !_cancelTokenSource.IsCancellationRequested)
                    if (_commandInvokation.KeyboardInputBuffer.TryDequeue(out var item))
                        return new[] { item };

                await Task.Delay(25).ConfigureAwait(false);
            }

            throw new TaskCanceledException();
        }

        /// <inheritdoc/>
        async Task<string> IKeyboardInput.ReadLineAsync(bool echo)
        {
            var sb = new StringBuilder();

            while (!_cancelTokenSource.IsCancellationRequested)
            {
                await _updateTerminalInputAsync().ConfigureAwait(false);

                while (_commandInvokation.KeyboardInputBuffer.Any() && !_cancelTokenSource.IsCancellationRequested)
                {
                    if (_commandInvokation.KeyboardInputBuffer.TryDequeue(out var item) && item.action == KeyAction.Character)
                    {
                        switch (item.character)
                        {
                            case '\r':
                                break; // Ignore

                            case '\n':
                                return sb.ToString();

                            default:
                                sb.Append(item.character);
                                break;
                        }
                    }
                }

                await Task.Delay(25).ConfigureAwait(false);
            }

            throw new TaskCanceledException();
        }

        /// <inheritdoc/>
        async Task<IEnumerable<(KeyAction action, char character)>> IKeyboardInput.TryReadAsync(bool echo)
        {
            await _updateTerminalInputAsync().ConfigureAwait(false);

            while (_commandInvokation.KeyboardInputBuffer.Any())
                if (_commandInvokation.KeyboardInputBuffer.TryDequeue(out var item))
                    return new[] { item };

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        #endregion
    }
}
