using SoftShell.Exceptions;
using SoftShell.Helpers;
using SoftShell.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;

namespace SoftShell.Execution
{
    internal partial class CommandChain : ICommandOutput, ICommandExceptionOutput
    {
        //private List<(string target, bool isAppend)> _outputAndErrorOutputTargets = new List<(string target, bool isAppend)>();
        private (string target, bool isAppend) _outputTarget = (target: null, isAppend: false);
        private (string target, bool isAppend) _errorOutputTarget = (target: null, isAppend: false);

        private FileStream _outputFile = null;
        private FileStream _errorOutputFile = null;
        private bool _isOutputLineTerminated = true;

        private ConcurrentQueue<char> _exceptionOutputCharacterQueue = new ConcurrentQueue<char>();

        private bool IsOutputNull
        {
            get
            {
                var outputRedirect = _redirects.FirstOrDefault(redir => redir.Type == RedirectType.Output);

                if (outputRedirect is null)
                    return false;

                return string.IsNullOrWhiteSpace(outputRedirect.Target);
            }
        }

        private bool IsErrorOutputNull
        {
            get
            {
                var errorOutputRedirect = _redirects.FirstOrDefault(redir => redir.Type == RedirectType.ErrorOutput);

                if (errorOutputRedirect is null)
                    return false;

                return string.IsNullOrWhiteSpace(errorOutputRedirect.Target);
            }
        }

        private bool IsRedirectedOutput => _redirects.Any(redir => redir.Type == RedirectType.Output);
        private bool IsRedirectedErrorOutput => _redirects.Any(redir => redir.Type == RedirectType.ErrorOutput);

        private void BeginWriteRedirectedOutputIfAny()
        {
            // Redirecting output to a file?
            if (IsRedirectedOutput && !IsOutputNull && _outputTarget.target != "&2")
            {
                // Append to an existing file?
                if (_outputTarget.isAppend && File.Exists(_outputTarget.target))
                {
                    _outputFile = File.Open(_outputTarget.target, FileMode.Append);
                }
                else // Create a new file
                {
                    _outputFile = File.Create(_outputTarget.target);
                }

                // Redirecting error output to the same file?
                if (_errorOutputTarget.target == "&1")
                {
                    _errorOutputFile = _outputFile;
                }
            }

            // Redirecting error output to a (separate) file?
            if ((_errorOutputFile is null) && IsRedirectedErrorOutput && !IsErrorOutputNull)
            {
                // Append to an existing file?
                if (_errorOutputTarget.isAppend && File.Exists(_errorOutputTarget.target))
                {
                    _errorOutputFile = File.Open(_errorOutputTarget.target, FileMode.Append);
                }
                else // Create a new file
                {
                    _errorOutputFile = File.Create(_errorOutputTarget.target);
                }

                // Redirecting output to the same file?
                if (_outputTarget.target == "&2")
                {
                    _outputFile = _errorOutputFile;
                }
            }
        }

        private string GetCommandErrorOutputText(string lineTermination)
        {
            var sb = new StringBuilder();

            foreach (var cmd in _commandsAndContexts)
            {
                while (cmd.context.ErrorOutputCharacterQueue.TryDequeue(out char ch))
                {
                    if (ch != '\r')
                    {
                        if (ch == '\n')
                            sb.Append(lineTermination);
                        else
                            sb.Append(ch);
                    }
                }
            }

            return sb.ToString();
        }

        private string GetExceptionOutputText(string lineTermination)
        {
            var sb = new StringBuilder();

            while (_exceptionOutputCharacterQueue.TryDequeue(out char ch))
            {
                if (ch != '\r')
                {
                    if (ch == '\n')
                        sb.Append(lineTermination);
                    else
                        sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        #region ICommandOutput

        /// </inheritdoc>
        bool ICommandOutput.IsPiped => false;

        /// </inheritdoc>
        int? ICommandOutput.WindowWidth => IsRedirectedOutput ? null : _session.TerminalWindowWidth;

        /// </inheritdoc>
        int? ICommandOutput.WindowHeight => IsRedirectedOutput ? null : _session.TerminalWindowHeight;

        /// </inheritdoc>
        string ICommandOutput.LineTermination => IsRedirectedOutput ? Environment.NewLine : _session.LineTermination;

        /// </inheritdoc>
        Task ICommandOutput.WriteAsync(string text)
        {
            if (IsOutputNull)
                return Task.CompletedTask;

            if (text.Length > 0)
                _isOutputLineTerminated = false;

            if (text.EndsWith(Environment.NewLine))
                _isOutputLineTerminated = true;

            if (IsRedirectedOutput)
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                return _outputFile.WriteAsync(bytes, 0, bytes.Length, CancelTokenSource.Token);
            }

            return _session.TerminalWriteAsync(text, CancelTokenSource.Token);
        }

        /// </inheritdoc>
        Task ICommandOutput.WriteLineAsync()
        {
            if (IsOutputNull)
                return Task.CompletedTask;

            _isOutputLineTerminated = true;

            if (IsRedirectedOutput)
            {
                var bytes = Encoding.UTF8.GetBytes(Environment.NewLine);
                return _outputFile.WriteAsync(bytes, 0, bytes.Length, CancelTokenSource.Token);
            }

            return _session.TerminalWriteLineAsync(CancelTokenSource.Token);
        }

        /// </inheritdoc>
        Task ICommandOutput.WriteLineAsync(string text)
        {
            if (IsOutputNull)
                return Task.CompletedTask;

            _isOutputLineTerminated = true;

            if (IsRedirectedOutput)
            {
                var bytes = Encoding.UTF8.GetBytes($"{text}{Environment.NewLine}");
                return _outputFile.WriteAsync(bytes, 0, bytes.Length, CancelTokenSource.Token);
            }

            return _session.TerminalWriteLineAsync(text, CancelTokenSource.Token);
        }

        /// </inheritdoc>
        Task ICommandOutput.ClearScreenAsync()
        {
            if (IsOutputNull)
                return Task.CompletedTask;

            _isOutputLineTerminated = true;

            if (IsRedirectedOutput)
            {
                return ((ICommandOutput)this).WriteLineAsync();
            }

            return _session.ClearScreenAsync(CancelTokenSource.Token);
        }

        /// </inheritdoc>
        async Task ICommandOutput.CommandOutputEndAsync()
        {
            if (!_isOutputLineTerminated)
                await ((ICommandOutput)this).WriteLineAsync().ConfigureAwait(false);

            // Get error output from commands
            var errorText = GetCommandErrorOutputText(IsRedirectedErrorOutput ? Environment.NewLine : _session.LineTermination);
            var exceptionText = GetExceptionOutputText(IsRedirectedErrorOutput ? Environment.NewLine : _session.LineTermination);

            var text = errorText + exceptionText;

            if (!string.IsNullOrWhiteSpace(text))
            {
                // If already cancelled, don't use the cancellation token
                var cancelToken = CancelTokenSource.IsCancellationRequested
                                        ? CancellationToken.None
                                        : CancelTokenSource.Token;

                if (IsRedirectedErrorOutput)
                {
                    await _errorOutputFile.WriteAsync(Encoding.UTF8.GetBytes(text), 0, text.Length, cancelToken).ConfigureAwait(false);
                }
                else
                {
                    await _session.SetTextColorAsync(ConsoleColor.Red, cancelToken).ConfigureAwait(false);
                    await _session.TerminalWriteLineAsync(text, cancelToken).ConfigureAwait(false);
                    await _session.SetTextColorAsync(null, cancelToken).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region ICommandExceptionOutput

        /// </inheritdoc>
        Task ICommandExceptionOutput.HandleExceptionAsync(CommandException exception)
        {
            // If any inner exception, use that - otherwise the CommandException itself
            var ex = exception.InnerException ?? exception;

            var text = Session.GetExceptionOutputText(ex, exception.Command, Environment.NewLine);

            foreach (var ch in text)
                _exceptionOutputCharacterQueue.Enqueue(ch);

            return Task.CompletedTask;
        }

        #endregion
    }
}
