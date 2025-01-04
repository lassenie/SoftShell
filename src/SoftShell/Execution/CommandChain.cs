using SoftShell.Commands;
using SoftShell.Helpers;
using SoftShell.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Execution
{
    /// <summary>
    /// A chain with one or more commands being executed.
    /// For a single command the chain only has this command.
    /// For piped commands the chain has multiple commands in the same sequence as the pipe.
    /// </summary>
    /// <remarks>
    /// The <see cref="ICommandInput"/> of this command chain is for the first command in the chain to read (non-piped) input.
    /// The <see cref="ICommandOutput"/> of this command chain is for the last command in the chain to write (non-piped) output.
    /// </remarks>
    internal partial class CommandChain : IDisposable
    {
        private Session _session;
        private CommandInvokation[] _commands;
        private Redirect[] _redirects;

        private List<(CommandInvokation command, ConcreteCommandExecutionContext context)> _commandsAndContexts = new List<(CommandInvokation command, ConcreteCommandExecutionContext context)>();

        /// <summary>
        /// Source for cancellation token used when cancelling execution of the command chain.
        /// </summary>
        internal CancellationTokenSource CancelTokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// Is the last command in the chain clearing the screen?
        /// </summary>
        public bool IsScreenClearedLast => _commands.Last().Command is ClearScreenCommand;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The session in which the commans chain is executed.</param>
        /// <param name="commands">The commands to be in the chain.</param>
        public CommandChain(Session session, params CommandInvokation[] commands)
            : this(session, commands, new Redirect[0])
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The session in which the commans chain is executed.</param>
        /// <param name="commands">The commands to be in the chain.</param>
        /// <param name="redirects">Redirects, if any.</param>
        public CommandChain(Session session, IEnumerable<CommandInvokation> commands, IEnumerable<Redirect> redirects)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
            _redirects = redirects?.ToArray() ?? throw new ArgumentNullException(nameof(redirects));

            if (!commands.Any())
                throw new Exception("No commands to execute.");

            SetupRedirectedIO();

            ReadRedirectedInputIfAny();
            BeginWriteRedirectedOutputIfAny();
        }

        public void Dispose()
        {
            // Close output redirection file if any
            if (_outputFile != null)
            {
                _outputFile.Dispose();

                // If same as the error output file, consider it disposed as well, so forget about it
                if (_errorOutputFile == _outputFile)
                    _errorOutputFile = null;

                _outputFile = null;
            }

            // Close error output redirection file if any
            if (_errorOutputFile != null)
            {
                _errorOutputFile.Dispose();
                _errorOutputFile = null;
            }
        }

        /// <summary>
        /// Runs the command chain, executing all commands in parallel.
        /// </summary>
        /// <remarks>
        /// When writing custom SoftShell commands, observe the following due to this parallelism:
        /// - Keep processing input until <see cref="ICommandInput.IsEnded"/> is true.
        /// - If the command should wait doing something until after the previous command in the chain has finished
        ///   the <see cref="ICommandInput.IsEnded"/> can be checked.
        /// </remarks>
        /// <returns></returns>
        public async Task RunAsync()
        {
            await _session.ReportCommandChainBeginningOfOutputAsync();

            await _session.TerminalFlushInputAsync(CancelTokenSource.Token).ConfigureAwait(false);

            var tasks = CreateAndRunCommandTasks();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (!IsScreenClearedLast)
                await _session.TerminalWriteLineAsync(CancelTokenSource.Token).ConfigureAwait(false);

            await _session.ReportCommandChainEndOfOutputAsync();
        }

        private void SetupRedirectedIO()
        {
            var multipleRedirectsOfAType = _redirects.GroupBy(redir => redir.Type).FirstOrDefault(grp => grp.Count() > 1);
            if (multipleRedirectsOfAType != null)
                throw new Exception($"There can be only one redirect of each type - error at position {multipleRedirectsOfAType.Skip(1).First().CommandLineStartPosition}.");

            // Iterate absolute output redirects
            foreach (var redir in _redirects.Where(r => ((r.Type == RedirectType.Output) || (r.Type == RedirectType.ErrorOutput)) && !r.Target.StartsWith("&")))
            {
                if (redir.Type == RedirectType.Output)
                    _outputTarget = (redir.Target, redir.IsAppend);
                else if (redir.Type == RedirectType.ErrorOutput)
                    _errorOutputTarget = (redir.Target, redir.IsAppend);

                if (_outputTarget.target == _errorOutputTarget.target)
                    throw new Exception($"Output and error output can only be redirected to the same file if using '> &2' or '2> &1' - error at position {redir.CommandLineStartPosition}.");
            }

            // Iterate "same file" output redirects
            foreach (var redir in _redirects.Where(r => ((r.Type == RedirectType.Output) || (r.Type == RedirectType.ErrorOutput)) && r.Target.StartsWith("&")))
            {
                switch (redir.Target)
                {
                    case "&1":
                        {
                            if (redir.Type == RedirectType.Output)
                                throw new Exception($"Cannot redirect output to itself (did you mean '> $2' or '2> $1'?) - error at position {redir.CommandLineStartPosition}.");

                            if (_outputTarget.target is null)
                                throw new Exception($"Attempt to redirect error output to the same as the output file, but no output redirect exists - error at position {redir.CommandLineStartPosition}.");

                            if (_outputTarget.isAppend != redir.IsAppend)
                                throw new Exception($"Mismatch in redirect rewrite/append - error at position {redir.CommandLineStartPosition}.");

                            _errorOutputTarget = (redir.Target, redir.IsAppend);
                        }
                        break;

                    case "&2":
                        {
                            if (redir.Type == RedirectType.ErrorOutput)
                                throw new Exception($"Cannot redirect error output to itself (did you mean '2> $1' or '> $2'?) - error at position {redir.CommandLineStartPosition}.");

                            if (_errorOutputTarget.target is null)
                                throw new Exception($"Attempt to redirect output to the same as the error output file, but no error output redirect exists - error at position {redir.CommandLineStartPosition}.");

                            if (_errorOutputTarget.isAppend != redir.IsAppend)
                                throw new Exception($"Mismatch in redirect rewrite/append - error at position {redir.CommandLineStartPosition}.");

                            _outputTarget = (redir.Target, redir.IsAppend);
                        }
                        break;

                    default:
                        throw new Exception($"Unknown output redirection target '{redir.Target}' (should be '&1' or '&2') - error at position {redir.CommandLineStartPosition}.");
                }
            }
        }

        private IEnumerable<Task> CreateAndRunCommandTasks()
        {
            var tasks = new List<Task>();

            ConcreteCommandExecutionContext previousContext = null;

            for (int i = 0; i < _commands.Length; i++)
            {
                if (CancelTokenSource.IsCancellationRequested)
                    break;

                CommandChainLink link = null;

                bool isFirst = previousContext == null;
                bool isLast = i == _commands.Length - 1;

                var context = new ConcreteCommandExecutionContext(_session, _commands[i], i, _session.TerminalWindowWidth, _session.TerminalWindowHeight, CancelTokenSource, UpdateTerminalInputAsync);

                // Set command input
                if (isFirst)
                {
                    context.Input = this;
                }
                else
                {
                    // Link current execution context with previous
                    link = new CommandChainLink(previousContext, context, _session.TerminalWindowWidth, _session.TerminalWindowHeight, _session.LineTermination);
                    context.Input = link;
                }

                // Set command output

                // Let the previous command output to this link object
                if (link != null)
                {
                    previousContext.Output = link;
                    previousContext.ExceptionOutput = link;
                }

                // The last command in the chain has the terminal output
                if (isLast)
                {
                    context.Output = this;
                    context.ExceptionOutput = this;
                }

                // Run the tasks and add it to the list
                _commandsAndContexts.Add((_commands[i], context));

                previousContext = context;
            }

            // Execute all commands
            foreach (var cmdCtx in _commandsAndContexts)
            {
                tasks.Add(cmdCtx.command.Command.RunAsync(cmdCtx.context, cmdCtx.command.CommandLine, cmdCtx.command.Tokens));
            }

            return tasks;
        }
    }
}
