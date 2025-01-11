using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command that writes standard text input to a specified file and passes the text to standard output.
    /// </summary>
    public class TeeCommand : StdCommand
    {
        /// <summary>
        /// Host interface for directory and file operations.
        /// </summary>
        public interface IHost
        {
            /// <summary>
            /// Creates a given directory if not already existing.
            /// </summary>
            void CreateDirectory(string path);

            /// <summary>
            /// Begins writing text to an output file.
            /// </summary>
            /// <param name="path">Path to the file.</param>
            /// <param name="append">Append to an existing file? If not, it creates a new file (overwriting an existing, if any).</param>
            /// <param name="encoding">Text encoding to use.</param>
            /// <param name="useBom">Write byte order mark?</param>
            /// <returns></returns>
            StreamWriter BeginFileWrite(string path, bool append, Encoding encoding, bool useBom);
        }

        private IHost _host;

        /// <inheritdoc/>
        protected override string Name => "tee";

        /// <inheritdoc/>
        public override string Description => "Writes text from standard input to a specified file (UTF-8 encoded) and passes the text to standard output.";

        /// <summary>
        /// Constructor that creates the command object using a default <see cref="IHost"/> implementation.
        /// </summary>
        internal TeeCommand() : this(new DefaultHost()) { }

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="IHost"/> implementation.
        /// </summary>
        internal TeeCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            HasRequiredParameter("file", "File to write the text to.", val => val);
            HasFlagOption("append", "Appends output to the file, rather than overwriting it.");
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (!args.TryGetAs<string>(0, out var path) || string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Missing file path.");

            _host.CreateDirectory(Path.GetDirectoryName(path));

            bool append = options.HasFlag("append");

            using (var file = _host.BeginFileWrite(path, append, Encoding.UTF8, false))
            {
                while (!context.Input.IsEnded)
                {
                    var text = await context.Input.ReadAsync().ConfigureAwait(false);

                    await file.WriteAsync(text).ConfigureAwait(false);
                    await context.Output.WriteAsync(text).ConfigureAwait(false);
                }
            }
        }

        private class DefaultHost : IHost
        {
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public StreamWriter BeginFileWrite(string path, bool append, Encoding encoding, bool useBom) => append ? File.AppendText(path) : File.CreateText(path);
        }
    }
}
