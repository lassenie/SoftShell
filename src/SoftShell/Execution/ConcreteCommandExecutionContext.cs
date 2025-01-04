using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SoftShell.IO;
using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace SoftShell.Execution
{
    /// <summary>
    /// Concrete context for command execution, based on the abstract <see cref="CommandExecutionContext"/> class.
    /// </summary>
    internal sealed class ConcreteCommandExecutionContext : CommandExecutionContext, ICommandErrorOutput
    {
        private ICommandErrorOutput _errorOutput = null;
        private int? _windowWidth;
        private int? _windowHeight;
        private bool _isErrorOutputLineTerminated = true;

        /// <summary>
        /// Character queue for error output characters from the command.
        /// </summary>
        public ConcurrentQueue<char> ErrorOutputCharacterQueue { get; } = new ConcurrentQueue<char>();

        /// <inheritdoc/>
        public override ICommandErrorOutput ErrorOutput => _errorOutput;

        /// <inheritdoc/>
        public ICommandExceptionOutput ExceptionOutput { get; set; } = null;

        /// <summary>
        /// Constructor, where the object provides its own error output.
        /// </summary>
        /// <param name="session">The session running the command.</param>
        /// <param name="commandInvokation">Command invocation information.</param>
        /// <param name="commandChainIndex">Index in the command chain (0 is first command).</param>
        /// <param name="windowWidth">Width as number of characters per line of the terminal. Null if unknown.</param>
        /// <param name="windowHeight">Height as number of lines of the terminal. Null if unknown.</param>
        /// <param name="cancelTokenSource">Cancellation token source that can be used for cancelling the command.</param>
        /// <param name="updateTerminalInputAsync">Delegate that can be used to asynchonously read buffered input from the terminal.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ConcreteCommandExecutionContext(ISession session, CommandInvokation commandInvokation, int commandChainIndex, int? windowWidth, int? windowHeight, CancellationTokenSource cancelTokenSource, Func<Task> updateTerminalInputAsync)
            : base(session, commandInvokation, commandChainIndex, cancelTokenSource, updateTerminalInputAsync)
        {
            _errorOutput = this;
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
        }

        /// <summary>
        /// Constructor, using a custom error output.
        /// </summary>
        /// <param name="session">The session running the command.</param>
        /// <param name="customErrorOutput">Custom error output to use.</param>
        /// <param name="commandInvokation">Command invocation information.</param>
        /// <param name="commandChainIndex">Index in the command chain (0 is first command).</param>
        /// <param name="windowWidth">Width as number of characters per line of the terminal. Null if unknown.</param>
        /// <param name="windowHeight">Height as number of lines of the terminal. Null if unknown.</param>
        /// <param name="cancelTokenSource">Cancellation token source that can be used for cancelling the command.</param>
        /// <param name="updateTerminalInputAsync">Delegate that can be used to asynchonously read buffered input from the terminal.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ConcreteCommandExecutionContext(ISession session, ICommandErrorOutput customErrorOutput, CommandInvokation commandInvokation, int commandChainIndex, int? windowWidth, int? windowHeight, CancellationTokenSource cancelTokenSource, Func<Task> updateTerminalInputAsync)
            : this(session, commandInvokation, commandChainIndex, windowWidth, windowHeight, cancelTokenSource, updateTerminalInputAsync)
        {
            _errorOutput = customErrorOutput;
        }

        #region ICommandErrorOutput

        /// <inheritdoc/>
        int? ICommandErrorOutput.WindowWidth => _windowWidth;

        /// <inheritdoc/>
        int? ICommandErrorOutput.WindowHeight => _windowHeight;

        /// <inheritdoc/>
        string ICommandErrorOutput.LineTermination => Environment.NewLine;

        /// <inheritdoc/>
        Task ICommandErrorOutput.WriteAsync(string text)
        {
            foreach (var ch in text)
            {
                _isErrorOutputLineTerminated = false;
                ErrorOutputCharacterQueue.Enqueue(ch);
            }

            if (text.EndsWith(Environment.NewLine))
                _isErrorOutputLineTerminated = true;

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        Task ICommandErrorOutput.WriteLineAsync()
        {
            foreach (var ch in Environment.NewLine)
                ErrorOutputCharacterQueue.Enqueue(ch);

            _isErrorOutputLineTerminated = true;

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        Task ICommandErrorOutput.WriteLineAsync(string text)
        {
            ((ICommandErrorOutput)this).WriteAsync(text).Wait(); // Takes no time

            return ((ICommandErrorOutput)this).WriteLineAsync();
        }

        /// <inheritdoc/>
        Task ICommandErrorOutput.CommandErrorOutputEndAsync()
        {
            // Terminate last line if needed

            if (_isErrorOutputLineTerminated)
                return Task.CompletedTask;
            else
                return ((ICommandErrorOutput)this).WriteLineAsync();
        }

        #endregion
    }
}
