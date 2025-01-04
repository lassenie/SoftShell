using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoftShell.Execution;
using SoftShell.IO;

namespace SoftShell
{
    /// <summary>
    /// Context interface for command execution.
    /// Injected in execution methods in <see cref="Command"/>-derived classes for accessing input/output etc.
    /// </summary>
    public interface ICommandExecutionContext
    {
        /// <summary>
        /// ID of the session in which the command is executed.
        /// </summary>
        int SessionId { get; }

        /// <summary>
        /// Can be checked to determine if execution is ended - successfully or not, for whatever reason.
        /// </summary>
        bool IsExecutionEnded { get; }

        /// <summary>
        /// Interface for receiving keyboard input.
        /// In case of piped commands, the keyboard input is only available to the last command
        /// in the pipe - check the <see cref="IKeyboardInput.IsAvailable"/> property before use.
        /// Remember also to check <see cref="IKeyboardInput.IsEnded"/> (true if cancelled).
        /// </summary>
        IKeyboardInput Keyboard { get; }

        /// <summary>
        /// Interface for receiving standard input to the command.
        /// Remember to check <see cref="ICommandInput.IsEnded"/> (true if cancelled).
        /// </summary>
        ICommandInput Input { get; }

        /// <summary>
        /// Interface for providing standard output from the command.
        /// </summary>
        ICommandOutput Output { get; }

        /// <summary>
        /// Interface for providing standard error output from the command.
        /// </summary>
        ICommandErrorOutput ErrorOutput { get; }

        /// <summary>
        /// The command object being executed.
        /// </summary>
        Command Command { get; }

        /// <summary>
        /// The command line provided for the command. In case of piped commands, only the command's part of entire command line is included.
        /// </summary>
        string CommandLine { get; }

        /// <summary>
        /// Parsed command line tokens.
        /// </summary>
        IEnumerable<CommandLineToken> CommandLineTokens { get; }

        /// <summary>
        /// Zero-based index of the command in the chain (pipe).
        /// </summary>
        int CommandChainIndex { get; }

        /// <summary>
        /// Cancellation token that will be signalled if the command execution is cancelled.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// True if command execution has been cancelled.
        /// </summary>
        bool IsCancellationRequested { get; }

        /// <summary>
        /// Cancels command execution. Non-blocking method.
        /// </summary>
        void RequestCancel();

        /// <summary>
        /// Asynchronously terminates the user session that the command is executing in.
        /// </summary>
        /// <param name="hard">Use hard termination, i.e. immediately terminate the session before the command ends?</param>
        /// <returns>A running task that performs the operation.</returns>
        Task TerminateSessionAsync(bool hard);
    }
}
