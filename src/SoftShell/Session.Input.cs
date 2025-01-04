using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoftShell.IO;
using static System.Net.Mime.MediaTypeNames;

namespace SoftShell
{
    internal partial class Session
    {
        internal Task TerminalFlushInputAsync(CancellationToken cancelToken)
        {
            return Task.Run(() =>
            {
                // Lock while the task runs
                lock (_terminalReadWriteLock)
                {
                    Terminal.FlushInputAsync(cancelToken).Wait();
                }
            });
        }

        internal Task<IEnumerable<(KeyAction action, char character)>> TerminalReadAsync(CancellationToken cancelToken, bool echo = true)
        {
            return Task.Run(() =>
            {
                // Lock while the task runs
                lock (_terminalReadWriteLock)
                {
                    return Terminal.ReadAsync(cancelToken, echo).Result;
                }
            });
        }

        internal Task<IEnumerable<(KeyAction action, char character)>> TerminalTryReadAsync(bool echo = true)
        {
            return Task.Run(() =>
            {
                // Lock while the task runs
                lock (_terminalReadWriteLock)
                {
                    return Terminal.TryReadAsync(echo).Result;
                }
            });
        }

        internal Task<string> TerminalReadLineAsync(CancellationToken cancelToken, bool echo = true)
        {
            return Task.Run(() =>
            {
                // Lock while the task runs
                lock (_terminalReadWriteLock)
                {
                    return Terminal.ReadLineAsync(cancelToken, echo, string.Empty, (action, character) => false).Result.strOut;
                }
            });
        }

        internal Task<(string strOut, KeyAction escapingAction, char escapingChar)> TerminalReadLineAsync(CancellationToken cancelToken, bool echo, string initialString, Func<KeyAction, char, bool> isEscapingCheck)
        {
            return Task.Run(() =>
            {
                // Lock while the task runs
                lock (_terminalReadWriteLock)
                {
                    return Terminal.ReadLineAsync(cancelToken, echo, initialString, isEscapingCheck).Result;
                }
            });
        }
    }
}
