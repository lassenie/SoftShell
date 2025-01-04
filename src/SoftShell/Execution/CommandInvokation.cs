using SoftShell.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftShell.Execution
{
    /// <summary>
    /// Information about an invokation of a command.
    /// </summary>
    internal sealed class CommandInvokation
    {
        /// <summary>
        /// The invoked command object.
        /// </summary>
        public Command Command { get; }

        /// <summary>
        /// The command line used for invoking the command.
        /// In case of piped commands, this property only contains the pieze of the total command line used for this command.
        /// </summary>
        public string CommandLine { get; }

        /// <summary>
        /// The starting character position of the command line.
        /// For a non-piped command, or the first command in a pipe, this will be 1.
        /// </summary>
        public int CommandLineStartPosition { get; }

        /// <summary>
        /// Parsed standard tokens of the command line, including the command itself.
        /// For custom commands that use special parsing the <see cref="CommandLine"/> property can be used for custom parsing.
        /// </summary>
        public IEnumerable<CommandLineToken> Tokens;

        /// <summary>
        /// Buffer for commnds to receive direct keyboard input through the <see cref="IKeyboardInput"/> interface.
        /// Keyboard input is passed to the buffer of all commands in a command chain (regardless if they use it or not).
        /// Certain commands may need to handle direct keyboard input rather than input from the <see cref="ICommandInput"/> interface
        /// (e.g. the 'More' command which can take both piped input and handle key presses from the keyboard). 
        /// </summary>
        internal ConcurrentQueue<(KeyAction action, char character)> KeyboardInputBuffer { get; } = new ConcurrentQueue<(KeyAction action, char character)>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The invoked command object.</param>
        /// <param name="commandLine">The command line used for invoking the command. See the <see cref="CommandLine"/> property for more details.</param>
        /// <param name="commandLineStartPosition">The starting character position of the command line. See the <see cref="CommandLineStartPosition"/> for more details.</param>
        /// <param name="tokens">Parsed standard tokens of the command line, including the command itself. See the <see cref="Tokens"/> property for more details.</param>
        public CommandInvokation(Command command,
                                 string commandLine,
                                 int commandLineStartPosition,
                                 IEnumerable<CommandLineToken> tokens)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            CommandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));
            Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            CommandLineStartPosition = commandLineStartPosition;
        }
    }
}
