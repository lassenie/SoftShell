using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoftShell.Exceptions;
using SoftShell.Helpers;
using SoftShell.IO;

using static System.Net.Mime.MediaTypeNames;

namespace SoftShell.Execution
{
    /// <summary>
    /// A link in the command chain. This ties together the output from one command to the input of next command.
    /// </summary>
    /// <remarks>
    /// The <see cref="ICommandInput"/> of this command chain link is for next command in the chain to read (piped) input.
    /// The <see cref="ICommandOutput"/> of this command chain link is for the previous command in the chain to write (piped) output.
    /// </remarks>
    internal class CommandChainLink : ICommandInput, ICommandOutput, ICommandExceptionOutput
    {
        private const int MaxCharacterQueueSize = 10240;

        private CommandExecutionContext _previousContext;
        private ConcreteCommandExecutionContext _nextContext;
        private int? _windowWidth;
        private int? _windowHeight;
        private string _lineTermination;

        private ConcurrentQueue<char> _characterQueue = new ConcurrentQueue<char>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="previousContext">The execution context of the previous command.</param>
        /// <param name="nextContext">The execution context of next command.</param>
        /// <param name="windowWidth">Width as number of characters per line of the terminal. Null if unknown.</param>
        /// <param name="windowHeight">Height as number of lines of the terminal. Null if unknown.</param>
        /// <param name="lineTermination">Line termination used by the terminal, i.e. "\n" or "\r\n".</param>
        public CommandChainLink(CommandExecutionContext previousContext, ConcreteCommandExecutionContext nextContext, int? windowWidth, int? windowHeight, string lineTermination)
        {
            _previousContext = previousContext ?? throw new ArgumentNullException(nameof(previousContext));
            _nextContext = nextContext ?? throw new ArgumentNullException(nameof(nextContext));
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
            _lineTermination = lineTermination ?? throw new ArgumentNullException(nameof(lineTermination));
        }

        #region ICommandInput

        /// <inheritdoc/>
        bool ICommandInput.IsPiped => true;

        /// <inheritdoc/>
        bool ICommandInput.IsEnded => (_previousContext.CancellationToken.IsCancellationRequested || _previousContext.IsExecutionEnded) && !_characterQueue.TryPeek(out var _);

        /// <inheritdoc/>
        Task ICommandInput.FlushInputAsync()
        {
            return Task.Run(() => { while (_characterQueue.TryDequeue(out char _)) ; });
        }

        /// <inheritdoc/>
        Task<string> ICommandInput.TryReadAsync()
        {
            return Task.Run(() => DoTryRead());
        }

        /// <inheritdoc/>
        Task<string> ICommandInput.ReadAsync()
        {
            return Task.Run(() => DoRead());
        }

        /// <inheritdoc/>
        Task<string> ICommandInput.ReadLineAsync()
        {
            return Task.Run(() => DoReadLine());
        }

        #endregion

        #region ICommandOutput

        /// <inheritdoc/>
        bool ICommandOutput.IsPiped => true;

        /// <inheritdoc/>
        int? ICommandOutput.WindowWidth => _windowWidth;

        /// <inheritdoc/>
        int? ICommandOutput.WindowHeight => _windowHeight;

        /// <inheritdoc/>
        string ICommandOutput.LineTermination => _lineTermination;

        /// <inheritdoc/>
        Task ICommandOutput.WriteAsync(string text)
        {
            return Task.Run(() => DoWrite(text));
        }

        /// <inheritdoc/>
        Task ICommandOutput.WriteLineAsync()
        {
            return Task.Run(() => DoWrite(_lineTermination));
        }

        /// <inheritdoc/>
        Task ICommandOutput.WriteLineAsync(string text)
        {
            return Task.Run(() =>
            {
                DoWrite(text);
                DoWrite(_lineTermination);
            });
        }

        /// <inheritdoc/>
        Task ICommandOutput.ClearScreenAsync()
        {
            // No way to clear the screen here - just insert line break
            return Task.Run(() =>
            {
                DoWrite(_lineTermination);
            });
        }

        /// <inheritdoc/>
        Task ICommandOutput.CommandOutputEndAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region ICommandExceptionOutput

        /// <inheritdoc/>
        Task ICommandExceptionOutput.HandleExceptionAsync(CommandException exception)
        {
            return _nextContext.Command.HandleExceptionAsync(_nextContext, exception);
        }

        #endregion

        private string DoRead()
        {
            while (true)
            {
                bool isPreviousCommandEnded = _previousContext.IsExecutionEnded; // Important: Get this boolean value before reading

                var str = DoTryRead();

                if (str != null)
                    return str;

                if (isPreviousCommandEnded)
                    return string.Empty;

                Thread.Sleep(5);
            }
        }

        private string DoTryRead()
        {
            if (_characterQueue.TryDequeue(out char ch))
                return ch.ToString();
            else
                return null;
        }

        private string DoReadLine()
        {
            var sb = new StringBuilder();
            char ch;

            while (!_previousContext.IsExecutionEnded)
            {
                while (!_characterQueue.TryDequeue(out ch) && !_previousContext.IsExecutionEnded)
                    Thread.Sleep(5);

                switch (ch)
                {
                    case '\r':
                        // Ignore
                        break;

                    case '\n':
                        return sb.ToString(); // End of line reached

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return string.Empty;
        }

        private void DoWrite(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            foreach (var ch in text)
            {
                CheckQueueLimit();

                if (_nextContext.CancellationToken.IsCancellationRequested)
                    return;
                
                _characterQueue.Enqueue(ch);
            }
        }

        private void CheckQueueLimit()
        {
            // Queue limit not reached?
            if (_characterQueue.Count < MaxCharacterQueueSize)
                return;

            // Wait till below queue limit (or cancelled)
            while (_characterQueue.Count >= MaxCharacterQueueSize && !_nextContext.CancellationToken.IsCancellationRequested)
                    Thread.Sleep(5);
        }
    }
}
