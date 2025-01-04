using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell
{
    /// <summary>
    /// Base class for terminal interface implementations.
    /// </summary>
    public abstract class TerminalInterface : ITerminalInterface
    {
        /// <inheritdoc/>
        public abstract string TerminalType { get; }

        /// <inheritdoc/>
        public abstract string TerminalInstanceInfo { get; }

        /// <inheritdoc/>
        public abstract string LineTermination { get; protected set; }

        /// <inheritdoc/>
        public abstract Encoding Encoding { get; protected set; }

        /// <inheritdoc/>
        public abstract int? WindowWidth { get; }

        /// <inheritdoc/>
        public abstract int? WindowHeight { get; }

        /// <inheritdoc/>
        public ConsoleColor? CurrentTextColor { get; protected set; } = null; // null = default

        /// <inheritdoc/>
        public event EventHandler TaskCancelRequest;

        /// <inheritdoc/>
        public virtual void Dispose() { }

        /// <inheritdoc/>
        public abstract Task FlushInputAsync(CancellationToken cancelToken);

        /// <inheritdoc/>
        public virtual async Task<IEnumerable<(KeyAction action, char character)>> ReadAsync(CancellationToken cancelToken, bool echo)
        {
            IEnumerable<(KeyAction action, char character)> readData;

            while (!cancelToken.IsCancellationRequested)
            {
                readData = await TryReadAsync(echo).ConfigureAwait(false);

                if (readData.Any())
                    return readData;
                else
                    await Task.Delay(10).ConfigureAwait(false);
            }

            throw new TaskCanceledException();
        }

        /// <inheritdoc/>
        public abstract Task<IEnumerable<(KeyAction action, char character)>> TryReadAsync(bool echo);

        /// <inheritdoc/>
        public abstract Task<(string strOut, KeyAction escapingAction, char escapingChar)> ReadLineAsync(CancellationToken cancelToken, bool echo, string initialString, Func<KeyAction, char, bool> isEscapingCheck);

        /// <inheritdoc/>
        public abstract Task WriteAsync(string text, CancellationToken cancelToken);

        /// <inheritdoc/>
        public virtual Task WriteLineAsync(string text, CancellationToken cancelToken)
            => WriteAsync(text + LineTermination, cancelToken);

        /// <inheritdoc/>
        public abstract Task ClearScreenAsync(CancellationToken cancelToken);

        /// <inheritdoc/>
        public abstract Task SetTextColorAsync(ConsoleColor? color, CancellationToken cancelToken);

        /// <inheritdoc/>
        public virtual Task ReportCommandChainBeginningOfOutputAsync()
        {
            // Do nothing in default implementation
            // (can be used for test implementations etc.)
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual Task ReportCommandChainEndOfOutputAsync()
        {
            // Do nothing in default implementation
            // (can be used for test implementations etc.)
            return Task.CompletedTask;
        }

        /// <summary>
        /// Fires the <see cref="TaskCancelRequest"/> event.
        /// </summary>
        protected void RequestTaskCancel()
        {
            var handler = TaskCancelRequest;

            handler?.Invoke(this, new EventArgs());
        }
    }
}
