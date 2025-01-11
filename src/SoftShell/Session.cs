using SoftShell.Commands;
using SoftShell.Commands.Anonymous;
using SoftShell.Execution;
using SoftShell.Helpers;
using SoftShell.IO;
using SoftShell.Parsing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace SoftShell
{
    /// <summary>
    /// Representation of a SoftShell user session.
    /// </summary>
    internal sealed partial class Session : ISession
    {
        private const int MaxCommandHistoryLength = 20;

        private CancellationTokenSource _sessionCancelTokenSource = new CancellationTokenSource();
        private List<string> _commandHistory= new List<string>();
        private object _terminalReadWriteLock = new object();

        private static int _lastSessionId = 0;

        /// <inheritdoc/>
        public int Id { get; }

        /// <inheritdoc/>
        public SessionState State { get; private set; } = SessionState.NotStarted;

        /// <inheritdoc/>
        public bool IsEnded => State == SessionState.Ended;

        /// <inheritdoc/>
        public string TerminalType => Terminal?.TerminalType ?? string.Empty;

        /// <inheritdoc/>
        public string TerminalInstanceInfo => Terminal?.TerminalInstanceInfo ?? string.Empty;

        /// <inheritdoc/>
        public int? TerminalWindowWidth => Terminal?.WindowWidth;

        /// <inheritdoc/>
        public int? TerminalWindowHeight => Terminal?.WindowHeight;

        /// <summary>
        /// Line termination, i.e. "\n" or "\r\n", used by the terminal running the session.
        /// </summary>
        public string LineTermination => Terminal?.LineTermination ?? "\n";

        /// <summary>
        /// Possible commands available for the session.
        /// </summary>
        internal IEnumerable<Command> Commands => Host.Commands;

        /// <inheritdoc/>
        public ISessionHost Host { get; }

        /// <summary>
        /// The terminal running the session.
        /// </summary>
        private ITerminalInterface Terminal { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">Host of the session.</param>
        /// <param name="terminal">The terminal initiating the session.</param>
        public Session(ISessionHost host, ITerminalInterface terminal)
        {
            Id = ++_lastSessionId;

            Host = host;
            Terminal = terminal;

            Terminal.TaskCancelRequest += Terminal_TaskCancelRequest;
        }

        /// <summary>
        /// Asynchronously runs the session.
        /// </summary>
        /// <returns>A running task the runs the session.</returns>
        public async Task RunAsync()
        {
            State = SessionState.Login;

            try
            {
                await TerminalWriteLineAsync($"SoftShell session ID: {Id}", _sessionCancelTokenSource.Token).ConfigureAwait(false);
                await TerminalWriteLineAsync(_sessionCancelTokenSource.Token).ConfigureAwait(false);

                if (await AuthenticateUserAsync().ConfigureAwait(false))
                {
                    State = SessionState.Running;

                    // Write session start info, if any

                    var infoLines = Host.GetSessionStartInfo() ?? Enumerable.Empty<string>();

                    foreach (var line in infoLines)
                        await TerminalWriteLineAsync(line ?? string.Empty, _sessionCancelTokenSource.Token).ConfigureAwait(false);

                    if (infoLines.Any())
                        await TerminalWriteLineAsync(_sessionCancelTokenSource.Token).ConfigureAwait(false);

                    // Handle commands until the session ends
                    while (State == SessionState.Running)
                    {
                        try
                        {
                            (var commands, var redirects) = await GetCommandsAsync().ConfigureAwait(false);

                            if (commands.Any())
                            {
                                using (var commandChain = new CommandChain(this, commands, redirects))
                                {
                                    void HandleTerminalTaskCancelRequest(object sender, EventArgs e) => commandChain.CancelTokenSource.Cancel();

                                    // Make sure the command chain is cancelled if the whole session is cancelled
                                    using (var sessionCancelRegistration = _sessionCancelTokenSource.Token.Register(() => commandChain.CancelTokenSource.Cancel()))
                                    {
                                        Terminal.TaskCancelRequest += HandleTerminalTaskCancelRequest;

                                        try
                                        {
                                            // Run the command chain
                                            await commandChain.RunAsync().ConfigureAwait(false);
                                        }
                                        finally
                                        {
                                            Terminal.TaskCancelRequest -= HandleTerminalTaskCancelRequest;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                try
                                {
                                    await SetTextColorAsync(ConsoleColor.Red, _sessionCancelTokenSource.Token).ConfigureAwait(false);
                                    await TerminalWriteLineAsync(GetExceptionOutputText(ex, null, LineTermination), _sessionCancelTokenSource.Token).ConfigureAwait(false);
                                }
                                catch { }
                                finally
                                {
                                    await SetTextColorAsync(null, _sessionCancelTokenSource.Token).ConfigureAwait(false);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            State = SessionState.Ended;
        }

        /// <inheritdoc/>
        public void RequestEnd()
        {
            if (State == SessionState.Ended)
                return;

            State = SessionState.Ending;
        }

        /// <summary>
        /// Ends the session and disposes resources. Blocking call that returns when the session is ended and disposed.
        /// </summary>
        public async void Dispose()
        {
            RequestEnd();
            _sessionCancelTokenSource.Cancel();

            while (State != SessionState.Ended)
                await Task.Delay(100).ConfigureAwait(false);

            if (Terminal != null)
            {
                Terminal.Dispose();
            }
        }

        private async void Terminal_TaskCancelRequest(object sender, EventArgs e)
        {
            // Write output if not the session itself that was cancelled
            if (!_sessionCancelTokenSource.IsCancellationRequested)
            {
                await TerminalWriteLineAsync("Cancelled!", _sessionCancelTokenSource.Token).ConfigureAwait(false);
                await TerminalWriteLineAsync(_sessionCancelTokenSource.Token).ConfigureAwait(false);
            }
        }

        private async Task<bool> AuthenticateUserAsync()
        {
            string userName = null;
            string password = null;

            if (!Host.UserAuthentication.WantsUserAuthentication)
                return true;

            if (Host.UserAuthentication.WantsUserName)
            {
                await TerminalWriteAsync("User name: ", _sessionCancelTokenSource.Token).ConfigureAwait(false);
                userName = await TerminalReadLineAsync(_sessionCancelTokenSource.Token, true).ConfigureAwait(false);
            }

            if (_sessionCancelTokenSource.IsCancellationRequested)
                return false;

            if (Host.UserAuthentication.WantsPassword)
            {
                await TerminalWriteAsync("Password: ", _sessionCancelTokenSource.Token).ConfigureAwait(false);
                password = await TerminalReadLineAsync(_sessionCancelTokenSource.Token, false).ConfigureAwait(false);
                await TerminalWriteLineAsync(_sessionCancelTokenSource.Token).ConfigureAwait(false);
            }

            if (_sessionCancelTokenSource.IsCancellationRequested)
                return false;

            var userValid = await Host.UserAuthentication.AuthenticateUserAsync(userName, password, _sessionCancelTokenSource.Token,
                                                                                new Progress<string>(text => TerminalWriteLineAsync(text, _sessionCancelTokenSource.Token).Wait())).ConfigureAwait(false);

            // Allow eventual status messages from the host to be shown before proceeding
            await Task.Delay(100, _sessionCancelTokenSource.Token);

            if (userValid)
            {
                await TerminalWriteLineAsync(_sessionCancelTokenSource.Token).ConfigureAwait(false);
            }
            else
            {
                await TerminalWriteLineAsync("User authentication failed!", _sessionCancelTokenSource.Token).ConfigureAwait(false);

                Host.TerminateSession(this.Id, hard: true);
            }

            return userValid;
        }

        private async void ShowCommandPrompt()
        {
            await TerminalWriteAsync("> ", _sessionCancelTokenSource.Token).ConfigureAwait(false);
        }

        private async Task<(IEnumerable<CommandInvokation> commands, IEnumerable<Redirect> redirects)> GetCommandsAsync()
        {
            try
            {
                var cancelTokenSrc = new CancellationTokenSource();

                ShowCommandPrompt();

                string fullRawCommandLine = string.Empty;
                KeyAction escapingAction;
                char dummyEscapingChar;

                // Point one element beyond newest in command history
                var commandHistoryIndex = _commandHistory.Count;

                do
                {
                    (fullRawCommandLine, escapingAction, dummyEscapingChar) = await TerminalReadLineAsync(_sessionCancelTokenSource.Token, true, fullRawCommandLine,
                                                                                                          (action, character) => ((action == KeyAction.ArrowUp && commandHistoryIndex > 0) ||
                                                                                                                               (action == KeyAction.ArrowDown))).ConfigureAwait(false);

                    switch (escapingAction)
                    {
                        case KeyAction.ArrowUp:
                            if (_commandHistory.Any())
                            {
                                // Take previous command in history, or the oldest
                                if (commandHistoryIndex > 0) commandHistoryIndex -= 1;
                                fullRawCommandLine = _commandHistory[commandHistoryIndex];
                            }
                            else
                            {
                                // No history
                                fullRawCommandLine = string.Empty;
                            }
                            break;

                        case KeyAction.ArrowDown:
                            if (_commandHistory.Any())
                            {
                                // Take next command in history, or one element beyond newest
                                if (commandHistoryIndex < _commandHistory.Count) commandHistoryIndex += 1;

                                // Get command in history, or empty string if beyond newest
                                fullRawCommandLine = (commandHistoryIndex < _commandHistory.Count) ? _commandHistory[commandHistoryIndex] : string.Empty;
                            }
                            else
                            {
                                // No history
                                fullRawCommandLine = string.Empty;
                            }
                            break;

                        default:
                            // OK, got command line
                            break;
                    }
                } while (escapingAction != KeyAction.None);

                fullRawCommandLine = fullRawCommandLine?.Trim() ?? string.Empty;

                // Add to command history
                if (fullRawCommandLine.Length > 0)
                {
                    var existingHistoryIndex = _commandHistory.IndexOf(fullRawCommandLine);

                    // Already in the history?
                    if (existingHistoryIndex >= 0)
                    {
                        // Remove from old position
                        _commandHistory.RemoveAt(existingHistoryIndex);
                    }
                    else
                    {
                        // Remove oldest?
                        while (_commandHistory.Count >= MaxCommandHistoryLength)
                            _commandHistory.RemoveAt(0);
                    }

                    // Add as latest
                    _commandHistory.Add(fullRawCommandLine);
                }

                var commands = new List<CommandInvokation>();
                var redirects = new List<Redirect>();

                if (!string.IsNullOrWhiteSpace(fullRawCommandLine))
                {
                    var fullVarExpandedCommandLine = ExpandCommandLineVariables(fullRawCommandLine);
                    (var allCommandTokens, var allRedirectTokens) = new CommandLineTokenizer().Tokenize(fullVarExpandedCommandLine);

                    foreach (var commandTokens in allCommandTokens)
                    {
                        if (!commandTokens.tokens.Any() || (commandTokens.tokens.First()?.Type != TokenType.Value))
                            throw new Exception($"Missing command at position {commandTokens.position}.");

                        var candidates = Host.Commands.Where(cmd => cmd.CommandNames.Any(name => name.Equals(commandTokens.tokens.First().Content, StringComparison.InvariantCultureIgnoreCase))).ToList();
                        var commandName = commandTokens.tokens.Any() ? commandTokens.tokens.First().Content : string.Empty;

                        if (candidates.Count == 1)
                        {
                            commands.Add(new CommandInvokation(candidates[0], commandTokens.text,
                                                               commandTokens.position, commandTokens.tokens));
                        }
                        else if (candidates.Count == 0)
                        {
                            throw new Exception($"Unknown command '{commandName}'.");
                        }
                        else
                        {
                            throw new Exception(Command.GetAmbiguousCommandExceptionText(commandName, candidates));
                        }
                    }

                    foreach (var redirectTokens in allRedirectTokens)
                    {
                        redirects.Add(new Redirect(redirectTokens.position, redirectTokens.tokens));
                    }
                }

                return (commands, redirects);
            }
            catch (Exception ex)
            {
                return (commands: new CommandInvokation[]
                        {
                            new CommandInvokation(new ExceptionCommand(ex), string.Empty,
                                                  1, Enumerable.Empty<CommandLineToken>())
                        },
                        redirects: new Redirect[0]);
            }
        }

        private string ExpandCommandLineVariables(string rawCommandLine)
        {
            var sb = new StringBuilder();

            bool foundPercent = false;
            string variableName = null;

            for (int i = 0; i < rawCommandLine.Length; ++i)
            {
                if (rawCommandLine[i] == '%')
                {
                    if (foundPercent)
                    {
                        // Escaped percent character?
                        if (string.IsNullOrEmpty(variableName))
                        {
                            sb.Append('%');
                        }
                        else
                        {
                            // Ending variable
                            var variableValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Process);
                            sb.Append(variableValue);
                        }

                        foundPercent = false;
                        variableName = null;
                    }
                    else
                    {
                        foundPercent = true;
                        variableName = string.Empty; // Starting to build a variable name
                    }
                }
                else
                {
                    if (foundPercent)
                    {
                        // Adding character to a variable name
                        variableName = variableName + rawCommandLine[i];
                    }
                    else
                    {
                        // Adding normal character
                        sb.Append(rawCommandLine[i]);
                    }
                }
            }

            // Found opening percent character that wasn't closed or escaped?
            if (foundPercent)
            {
                if (string.IsNullOrEmpty(variableName))
                    sb.Append('%'); // Unescaped percent character - add as-is
                else
                    sb.Append(variableName); // Unclosed variable name - add as-is
            }

            return sb.ToString();
        }
    }
}
