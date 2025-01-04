using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;
using SoftShell.IO;
using SoftShell.Execution;
using SoftShell.Exceptions;
using System.Drawing;

namespace SoftShell
{
    internal partial class Session
    {
        public static string GetExceptionOutputText(Exception exception, Command command, string lineTermination)
        {
            var sb = new StringBuilder();

            void AddExceptionLine(Exception ex)
            {
                var exceptionTypePrefix = ex is Exception ?  string.Empty : $"{exception.GetType().Name}: ";

                sb.Append($"{exceptionTypePrefix}{ex.Message}");
                sb.Append(lineTermination);
            }

            if (exception is null)
                return string.Empty;

            if (!string.IsNullOrEmpty(command?.Name))
                sb.Append($"{command.Name}: ");

            if ((exception is AggregateException aggregateException) && aggregateException.InnerExceptions.Any())
            {
                var innerExceptions = aggregateException.InnerExceptions.ToArray();

                for (var i = 0; i < innerExceptions.Length; ++i)
                {
                    AddExceptionLine(innerExceptions[i]);
                }
            }
            else
            {
                AddExceptionLine(exception);
            }

            return sb.ToString();
        }

        internal Task TerminalWriteAsync(string text, CancellationToken cancelToken)
        {
            // Lock while the task runs
            lock (_terminalReadWriteLock)
            {
                return Terminal.WriteAsync(text, cancelToken);
            }
        }

        internal Task TerminalWriteAsync(Exception exception, Command command, CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                // Lock while the task runs
                lock (_terminalReadWriteLock)
                {
                    var oldColor = Terminal.CurrentTextColor;

                    try
                    {
                        Terminal.SetTextColorAsync(ConsoleColor.Red, cancelToken).Wait();
                        TerminalWriteAsync(GetExceptionOutputText(exception, command, Terminal.LineTermination), cancelToken).Wait();
                    }
                    finally
                    {
                        Terminal.SetTextColorAsync(oldColor, cancelToken).Wait();
                    }
                }
            });
        }

        internal Task TerminalWriteLineAsync(CancellationToken cancelToken) => TerminalWriteLineAsync(string.Empty, cancelToken);

        internal Task TerminalWriteLineAsync(string text, CancellationToken cancelToken)
        {
            // Lock while the task runs
            lock (_terminalReadWriteLock)
            {
                return Terminal.WriteLineAsync(text, cancelToken);
            }
        }

        internal Task ClearScreenAsync(CancellationToken cancelToken)
        {
            // Lock while the task runs
            lock (_terminalReadWriteLock)
            {
                return Terminal.ClearScreenAsync(cancelToken);
            }
        }

        internal Task SetTextColorAsync(ConsoleColor? color, CancellationToken cancelToken)
        {
            // Lock while the task runs
            lock (_terminalReadWriteLock)
            {
                return Terminal.SetTextColorAsync(color, cancelToken);
            }
        }

        /// <inheritdoc/>
        internal Task ReportCommandChainBeginningOfOutputAsync()
        {
            // Lock while the task runs
            lock (_terminalReadWriteLock)
            {
                return Terminal.ReportCommandChainBeginningOfOutputAsync();
            }
        }

        /// <inheritdoc/>
        internal Task ReportCommandChainEndOfOutputAsync()
        {
            // Lock while the task runs
            lock (_terminalReadWriteLock)
            {
                return Terminal.ReportCommandChainEndOfOutputAsync();
            }
        }
    }
}
