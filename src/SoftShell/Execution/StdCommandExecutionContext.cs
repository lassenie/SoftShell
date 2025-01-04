using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoftShell.IO;

namespace SoftShell.Execution
{
    /// <summary>
    /// Context for execution of <see cref="StdCommand"/>-based commands.
    /// Used for accessing input/output etc.
    /// </summary>
    internal class StdCommandExecutionContext : IStdCommandExecutionContext
    {
        private ICommandExecutionContext _innerContext;

        /// <inheritdoc/>
        public int SessionId => _innerContext.SessionId;

        /// <inheritdoc/>
        public bool IsExecutionEnded => _innerContext.IsExecutionEnded;

        /// <inheritdoc/>
        public IKeyboardInput Keyboard => _innerContext.Keyboard;

        /// <inheritdoc/>
        public ICommandInput Input => _innerContext.Input;

        /// <inheritdoc/>
        public ICommandOutput Output => _innerContext.Output;

        /// <inheritdoc/>
        public ICommandErrorOutput ErrorOutput => _innerContext.ErrorOutput;

        /// <inheritdoc/>
        public Command Command => _innerContext.Command;

        /// <inheritdoc/>
        public string CommandLine => _innerContext.CommandLine;

        /// <inheritdoc/>
        public IEnumerable<CommandLineToken> CommandLineTokens => _innerContext.CommandLineTokens;

        /// <inheritdoc/>
        public int CommandChainIndex => _innerContext.CommandChainIndex;

        /// <inheritdoc/>
        public CancellationToken CancellationToken => _innerContext.CancellationToken;

        /// <inheritdoc/>
        public bool IsCancellationRequested => _innerContext.IsCancellationRequested;

        /// <inheritdoc/>
        public Subcommand Subcommand { get; }

        /// <inheritdoc/>
        public CommandArgs Args { get; }

        /// <inheritdoc/>
        public CommandOptions Options { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="innerContext">The basic command execution context used for any commands.</param>
        /// <param name="subcommand">Subcommand (or non-subcommand) to execute.</param>
        /// <param name="args">Parsed arguments.</param>
        /// <param name="options">Parsed options (values and/or flags).</param>
        public StdCommandExecutionContext(ICommandExecutionContext innerContext, Subcommand subcommand, CommandArgs args, CommandOptions options)
        {
            _innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
            Subcommand = subcommand ?? throw new ArgumentNullException(nameof(subcommand));
            Args = args ?? throw new ArgumentNullException(nameof(args));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public void RequestCancel() => _innerContext.RequestCancel();

        /// <inheritdoc/>
        public Task TerminateSessionAsync(bool hard) => _innerContext.TerminateSessionAsync(hard);
    }
}
