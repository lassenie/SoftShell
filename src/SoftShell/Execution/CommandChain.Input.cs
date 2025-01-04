using SoftShell.Exceptions;
using SoftShell.Helpers;
using SoftShell.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Execution
{
    internal partial class CommandChain : ICommandInput
    {
        private ConcurrentQueue<char> _inputQueue = new ConcurrentQueue<char>();
        private SemaphoreSlim _terminalInputLock = new SemaphoreSlim(1, 1);

        private bool IsInputNull
        {
            get
            {
                var inputRedirect = _redirects.FirstOrDefault(redir => redir.Type == RedirectType.Input);

                if (inputRedirect is null)
                    return false;

                return string.IsNullOrWhiteSpace(inputRedirect.Source);

            }
        }

        private bool IsRedirectedInput => _redirects.Any(redir => redir.Type == RedirectType.Input);

        private void ReadRedirectedInputIfAny()
        {
            if (!IsInputNull)
            {
                var inputRedirect = _redirects.FirstOrDefault(redir => redir.Type == RedirectType.Input);

                if (inputRedirect is null)
                    return;

                try
                {
                    using (var inputStream = File.OpenRead(inputRedirect.Source))
                    {
                        var fileSize = inputStream.Length;

                        var readBuffer = new byte[fileSize];

                        var bytesRead = inputStream.Read(readBuffer, 0, (int)fileSize);

                        if (bytesRead < fileSize)
                            throw new Exception($"Could not find or read command input file '{inputRedirect.Source}'.");

                        // Empty file? OK
                        if (bytesRead == 0)
                            return;

                        // Detect encoding
                        var encoding = EncodingDetector.GetTextEncoding(readBuffer);

                        if (encoding is null)
                            throw new Exception($"Could not decode text from the command input file '{inputRedirect.Source}'.");

                        // Enqueue characters from the file
                        foreach (var ch in encoding.GetChars(readBuffer))
                            _inputQueue.Enqueue(ch);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Could not find or read command input file '{inputRedirect.Source}'.", ex);
                }
            }
        }

        private async Task UpdateTerminalInputAsync()
        {
            try
            {
                // Lock semaphore to make sure the terminal input is queued in the right sequence in case multiple command threads execute this method
                await _terminalInputLock.WaitAsync(CancelTokenSource.Token).ConfigureAwait(false); ;

                try
                {
                    var terminalInput = await _session.TerminalTryReadAsync(false).ConfigureAwait(false);

                    // Not using redirected input?
                    if (!IsRedirectedInput)
                    {
                        // Add to input queue
                        foreach (var inputItem in terminalInput)
                            if (inputItem.action == KeyAction.Character)
                                _inputQueue.Enqueue(inputItem.character);
                    }

                    // Add to keyboard input buffer of all commands in the chain
                    foreach (var cmd in _commands)
                    {
                        foreach (var inputItem in terminalInput)
                            cmd.KeyboardInputBuffer.Enqueue(inputItem);
                    }
                }
                finally
                {
                    _terminalInputLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // OK - we didn't get to lock the semaphore because of cancellation
            }
        }

        #region ICommandInput

        /// </inheritdoc>
        bool ICommandInput.IsEnded => (IsRedirectedInput && _inputQueue.IsEmpty) || CancelTokenSource.IsCancellationRequested;

        /// </inheritdoc>
        bool ICommandInput.IsPiped => false;

        /// </inheritdoc>
        async Task ICommandInput.FlushInputAsync()
        {
            if (IsInputNull)
                return;

            await UpdateTerminalInputAsync().ConfigureAwait(false);

            while (_inputQueue.Any() && !CancelTokenSource.IsCancellationRequested)
                _inputQueue.TryDequeue(out var _);
        }

        /// </inheritdoc>
        async Task<string> ICommandInput.ReadAsync()
        {
            if (IsInputNull)
                return string.Empty;

            while (!CancelTokenSource.IsCancellationRequested)
            {
                await UpdateTerminalInputAsync().ConfigureAwait(false);

                while (_inputQueue.Any() && !CancelTokenSource.IsCancellationRequested)
                    if (_inputQueue.TryDequeue(out var ch))
                        return ch.ToString();

                await Task.Delay(25).ConfigureAwait(false);
            }

            throw new TaskCanceledException();
        }

        /// </inheritdoc>
        async Task<string> ICommandInput.ReadLineAsync()
        {
            if (IsInputNull)
                return string.Empty;

            var sb = new StringBuilder();

            while (!CancelTokenSource.IsCancellationRequested)
            {
                await UpdateTerminalInputAsync().ConfigureAwait(false);

                while (_inputQueue.Any() && !CancelTokenSource.IsCancellationRequested)
                {
                    if (_inputQueue.TryDequeue(out var ch))
                    {
                        switch (ch)
                        {
                            case '\r':
                                break; // Ignore

                            case '\n':
                                return sb.ToString();

                            default:
                                sb.Append(ch);
                                break;
                        }
                    }
                }

                await Task.Delay(25).ConfigureAwait(false);
            }

            throw new TaskCanceledException();
        }

        /// </inheritdoc>
        async Task<string> ICommandInput.TryReadAsync()
        {
            if (IsInputNull)
                return string.Empty;

            await UpdateTerminalInputAsync().ConfigureAwait(false);

            while (_inputQueue.Any())
                if (_inputQueue.TryDequeue(out var ch))
                    return ch.ToString();

            return string.Empty;
        }

        #endregion
    }
}
