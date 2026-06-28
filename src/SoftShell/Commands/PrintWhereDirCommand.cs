using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.Commands
{
    internal class PrintWhereDirCommand : StdCommand
    {
        /// <summary>
        /// Host interface for accessing the current working directory. Can be mocked for unit testing.
        /// </summary>
        public interface IHost
        {
            /// <summary>
            /// Gets the current working directory of the application.
            /// Wraps the <see cref="Directory.GetCurrentDirectory"/> method in the default implementation.
            /// </summary>
            string GetCurrentDirectory();
        }

        private IHost _host;

        protected override string Name => "pwd";

        public override string Description => "Prints the current working directory of the application.";

        /// <summary>
        /// Constructor that creates the command object using a default <see cref="IHost"/> implementation.
        /// </summary>
        internal PrintWhereDirCommand() : this(new DefaultHost()) { }

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="IHost"/> implementation.
        /// </summary>
        internal PrintWhereDirCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            return context.Output.WriteLineAsync(_host.GetCurrentDirectory());
        }

        private class DefaultHost : IHost
        {
            public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
        }
    }
}
