using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SoftShell.Exceptions;
using SoftShell.Execution;
using SoftShell.Helpers;
using SoftShell.IO;

namespace SoftShell
{
    /// <summary>
    /// Base class for all commands.
    /// </summary>
    public abstract class Command
    {
        /// <summary>
        /// The pure name of the command.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Variants of the command's name - both the pure name and fully qualified name with group prefix.
        /// </summary>
        public IEnumerable<string> Names
        {
            get
            {
                if (Group.IsCore) return new[] { Name, $".{Name}", $"{Group.Prefix}.{Name}" };

                return new[] { Name, $"{Group.Prefix}.{Name}" };
            }
        }

        // Short description of the command.
        public abstract string Description { get; }

        // The group that the command belongs to.
        public CommandGroup Group { get; internal set; }

        /// <summary>
        /// Is the command anonymous, i.e. not available to users?
        /// </summary>
        public virtual bool IsAnonymous => false;

        /// <summary>
        /// Gets help text for the command to be shown by the help system.
        /// </summary>
        /// <remarks>
        /// It may be relevant to have the help text formatted according to the terminal's
        /// window width. This information can be obtained through the
        /// context.Output.WindowWidth property (null if unavailable).
        /// </remarks>
        /// <param name="context">Command execution context.</param>
        /// <param name="subcommandName">
        /// Name of a subcommand to get the help text for.
        /// Null if no subcommand.
        /// </param>
        /// <returns>Full help text including info about parameters/options, if any.</returns>
        public abstract string GetHelpText(ICommandExecutionContext context, string subcommandName);

        /// <summary>
        /// Helper method to get an exception message text for an ambiguous command.
        /// </summary>
        /// <param name="commandName">Ambiguous name of the command given.</param>
        /// <param name="candidates">Possible commands that the given command could match.</param>
        /// <returns>Exception message text.</returns>
        public static string GetAmbiguousCommandExceptionText(string commandName, IEnumerable<Command> candidates)
        {
            string GetCommandNameText(Command command)
            {
                if (command.Group.IsCore)
                    return $"[{command.Group.Prefix}].{command.Name}";
                else
                    return $"{command.Group.Prefix}.{command.Name}";
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Ambiguous command '{commandName}' - please qualify. Possible commands:");

            foreach (var line in TextFormatting.GetAlignedColumnStrings(candidates,
                                                                        " ",
                                                                        ("", cmd => $"  {GetCommandNameText(cmd)}:", TextAlignment.Start),
                                                                        ("", cmd => cmd.Description, TextAlignment.Start)))
            {
                sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd(new[] { '\r', '\n' });
        }

        /// <summary>
        /// Internal post-construction of the command object after creation.
        /// </summary>
        internal virtual void PostConstruct() { }

        /// <summary>
        /// Internal method that is invoked when the command should run.
        /// The method calls the overridable <see cref="ExecuteAsync(ICommandExecutionContext, string, IEnumerable{CommandLineToken})"/>
        /// method and handles exceptions.
        /// </summary>
        /// <param name="context">Command execution context.</param>
        /// <param name="commandLine">
        /// The raw command line given to invoke the command.
        /// If the command is invoked in a pipe, only this command's part of the total command line is provided.
        /// No command line is provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <param name="tokens">
        /// Parsed tokens from the command line. First token is the command itself.
        /// No tokens are provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <returns>A task that runs the command.</returns>
        internal async Task RunAsync(ConcreteCommandExecutionContext context, string commandLine, IEnumerable<CommandLineToken> tokens)
        {
            CommandException exception = null;

            try
            {
                await ExecuteAsync(context, commandLine, tokens).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!context.IsCancellationRequested)
                    exception = new CommandException(ex, this, commandLine);
            }

            Debug.WriteLine("RunAsync: After ExecuteAsync");

            if (context.IsCancellationRequested && (exception is null))
                exception = new CommandCancelledException(this, commandLine);

            // Important: Set before eventually calling HandleExceptionAsync
            context.IsExecutionEnded = true;

            if (exception != null)
                await HandleExceptionAsync(context, exception).ConfigureAwait(false);

            await context.Output.CommandOutputEndAsync().ConfigureAwait(false);
            await context.ErrorOutput.CommandErrorOutputEndAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Internal method that handles exceptions thrown during execution of commands earlier in a command chain (pipe).
        /// The command itself is then cancelled, and the exception is passed on in the chain.
        /// </summary>
        /// <param name="context">Command execution context.</param>
        /// <param name="exception">Exception received.</param>
        /// <returns>A task that handles the exception.</returns>
        internal async Task HandleExceptionAsync(ConcreteCommandExecutionContext context, CommandException exception)
        {
            // Pass on the exception to the error output (next in the command chain or screen error output)
            await context.ExceptionOutput.HandleExceptionAsync(exception).ConfigureAwait(false);

            // Cancel this command?
            if (!context.IsExecutionEnded)
            {
                await context.CancelCommandAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Overridable method for executing the command.
        /// </summary>
        /// <param name="context">Command execution context.</param>
        /// <param name="commandLine">
        /// The raw command line given to invoke the command.
        /// If the command is invoked in a pipe, only this command's part of the total command line is provided.
        /// No command line is provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <param name="tokens">
        /// Parsed tokens from the command line. First token is the command itself.
        /// No tokens are provided for for anonymous commands, since they are not user-invoked.
        /// </param>
        /// <exception cref="Exception">A command may throw an exception in case of a run-time error.</exception>
        /// <returns>A task for executing the command.</returns>
        protected abstract Task ExecuteAsync(ICommandExecutionContext context, string commandLine, IEnumerable<CommandLineToken> tokens);
    }
}
