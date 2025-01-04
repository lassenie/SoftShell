using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.Commands.Anonymous
{
    /// <summary>
    /// Internal command class executed for throwing exceptions in case of invocation errors.
    /// The command is anonymous, i.e. not available to users.
    /// </summary>
    internal class ExceptionCommand : Command
    {
        private Exception _exception;

        /// <inheritdoc/>
        public override string Name => string.Empty;

        /// <inheritdoc/>
        public override string Description => string.Empty;

        /// <inheritdoc/>
        public override bool IsAnonymous => true;

        /// <summary>
        /// Constructor taking an exception.
        /// </summary>
        /// <param name="exception">Exception to throw when the command executes.</param>
        public ExceptionCommand(Exception exception)
        {
            _exception = exception;
        }

        /// <summary>
        /// Constructor taking a message.
        /// </summary>
        /// <param name="exception">Message to throw as an exception when the command executes.</param>
        public ExceptionCommand(string message)
        {
            _exception = new Exception(message);
        }

        /// <inheritdoc/>
        public override string GetHelpText(ICommandExecutionContext context, string subcommand) => string.Empty;

        /// <inheritdoc/>
        protected override Task ExecuteAsync(ICommandExecutionContext context, string commandLine, IEnumerable<CommandLineToken> tokens)
        {
            throw _exception ?? new Exception("Internal error.");
        }
    }
}
