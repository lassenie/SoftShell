using SoftShell.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SoftShell.Commands.HelpCommand;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command that lists current SoftShell sessions. 
    /// </summary>
    public class SessionCommand : StdCommand
    {
        /// <summary>
        /// Interface for providing session objects.
        /// </summary>
        public interface ISessionCollectionProvider
        {
            /// <summary>
            /// Gets current sessions.
            /// </summary>
            IEnumerable<ISession> GetSessions();
        }

        private ISessionCollectionProvider _sessionCollectionProvider;

        /// <inheritdoc/>
        protected override string Name => "session";

        /// <inheritdoc/>
        public override string Description => "Lists current SoftShell sessions.";

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="ISessionCollectionProvider"/> implementation.
        /// </summary>
        internal SessionCommand(ISessionCollectionProvider provider)
        {
            _sessionCollectionProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            var sessions = _sessionCollectionProvider.GetSessions()?.OrderBy(s => s.Id).ToList() ?? throw new Exception("Failed to get information about sessions.");

            await context.Output.WriteLineAsync($"Current SoftShell sessions:").ConfigureAwait(false);

            bool IsThis(ISession session) => session.Id == context.SessionId;

            string GetWindowSize(ISession s) => $"{(s.TerminalWindowWidth?.ToString() ?? "?")} x {(s.TerminalWindowHeight?.ToString() ?? "?")}";

            var lines = TextFormatting.GetAlignedColumnStrings(sessions,
                                                               "  ",
                                                               ("ID",          s => (IsThis(s) ? "*  " : "") + s.Id,                       TextAlignment.End),
                                                               ("State",       s => s.State,                                               TextAlignment.Start),
                                                               ("Type",        s => s.TerminalType,                                        TextAlignment.Start),
                                                               ("Terminal",    s => s.TerminalInstanceInfo + (IsThis(s) ? " (this)" : ""), TextAlignment.Start),
                                                               ("Window size", s => GetWindowSize(s),                                      TextAlignment.Start));

            foreach (var line in lines)
                await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
