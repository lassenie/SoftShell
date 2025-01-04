using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command that displays file or piped input contents one screen at a time.
    /// </summary>
    public class MoreCommand : StdCommand
    {
        private class ExecutionState
        {
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public int CurrentLinePosition { get; set; }
            public int CurrentPageLinesShown { get; set; }
            public Action CurrrentAction { get; set; }
        }

        private enum Action
        {
            ShowPage,
            ShowLine,
            Quit
        }

        private const int DefaultWindowWidth = 80;
        private const int DefaultWindowHeight = 25;

        /// <inheritdoc/>
        public override string Name => "More";

        /// <inheritdoc/>
        public override string Description => "Displays file or piped input contents one screen at a time.";

        /// <summary>
        /// Constructor that creates the command object.
        /// </summary>
        internal MoreCommand()
        {
            HasOptionalParameter("file", "File to show contents for.", val => val);
        }

        /// <inheritdoc/>
        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            if (!context.Keyboard.IsAvailable)
                throw new Exception($"The {this.Name} command must be the last in a pipe in order to receive keyboard input.");

            var exeState = new ExecutionState
            {
                WindowWidth = context.Output.WindowWidth ?? DefaultWindowWidth,
                WindowHeight = context.Output.WindowHeight ?? DefaultWindowHeight,
                CurrentLinePosition = 0,
                CurrentPageLinesShown = 0,
                CurrrentAction = Action.ShowPage
            };

            if (args.TryGetAs<string>(0, out var file))
                return ShowFileContentsAsync(context, exeState, file);
            else
                return ShowPipedInputAsync(context, exeState);
        }

        private async Task ShowFileContentsAsync(IStdCommandExecutionContext context, ExecutionState exeState, string file)
        {
            if (!File.Exists(file))
                throw new Exception($"File {file} not found.");

            using (var reader = new StreamReader(File.OpenRead(file)))
            {
                const int FileReadBufferSize = 256;

                var readBuffer = new char[FileReadBufferSize];

                while (!reader.EndOfStream &&
                       !context.CancellationToken.IsCancellationRequested &&
                       (exeState.CurrrentAction != Action.Quit))
                {
                    var numberOfChars = reader.ReadBlock(readBuffer, 0, readBuffer.Length);

                    await OutputTextAsync(context, exeState, new string(readBuffer, 0, numberOfChars)).ConfigureAwait(false);
                }
            }
        }

        private async Task ShowPipedInputAsync(IStdCommandExecutionContext context, ExecutionState exeState)
        {
            if (!context.Input.IsPiped)
                throw new Exception("Without a file argument a piped input is expected for this command.");

            while (!context.Input.IsEnded &&
                   (exeState.CurrrentAction != Action.Quit))
            {
                var text = context.Input.ReadAsync().Result;

                if (text != null)
                {
                    await OutputTextAsync(context, exeState, text).ConfigureAwait(false);
                }
            }
        }

        const int WidthEndMargin = 1;
        const int HeightEndMargin = 1;

        private async Task OutputTextAsync(IStdCommandExecutionContext context, ExecutionState exeState, string text)
        {
            foreach (var ch in text)
            {
                if (context.CancellationToken.IsCancellationRequested ||
                    exeState.CurrrentAction == Action.Quit)
                    return;

                await OutputCharAsync(context, exeState, ch).ConfigureAwait(false);
            }
        }

        private async Task OutputCharAsync(IStdCommandExecutionContext context, ExecutionState exeState, char ch)
        {
            // Character to be ignored?
            if (ch == '\r' || ((ch != '\n') && char.IsControl(ch)))
                return;

            // Replace any other whitespace than new-line with space
            if ((ch != '\n') && char.IsWhiteSpace(ch))
                ch = ' ';

            if (ch == '\n')
            {
                exeState.CurrentPageLinesShown++;
                exeState.CurrentLinePosition = 0;
            }
            else
            {
                // Reaching the end of the line? Then enforce a line wrap.
                if (exeState.CurrentLinePosition >= exeState.WindowWidth - WidthEndMargin)
                    await OutputCharAsync(context, exeState, '\n').ConfigureAwait(false);

                exeState.CurrentLinePosition++;
            }

            if (ch == '\n')
            {
                await context.Output.WriteLineAsync().ConfigureAwait(false);

                var numberOfLinesToShow = (exeState.CurrrentAction == Action.ShowPage)
                                              ? exeState.WindowHeight - HeightEndMargin
                                              : 1;

                if (exeState.CurrentPageLinesShown >= numberOfLinesToShow)
                {
                    exeState.CurrrentAction = await ShowMorePromptAsync(context).ConfigureAwait(false);
                    exeState.CurrentPageLinesShown = 0;
                }
            }
            else
                await context.Output.WriteAsync(ch.ToString()).ConfigureAwait(false);
        }

        private async Task<Action> ShowMorePromptAsync(IStdCommandExecutionContext context)
        {
            const string Prompt = "-- More -- ";

            async Task ClearPromptAsync()
            {
                await context.Output.WriteAsync(string.Empty.PadRight(Prompt.Length, '\b')).ConfigureAwait(false);
                await context.Output.WriteAsync(string.Empty.PadRight(Prompt.Length)).ConfigureAwait(false);
                await context.Output.WriteAsync(string.Empty.PadRight(Prompt.Length, '\b')).ConfigureAwait(false);
            }

            if (context.Keyboard.IsAvailable)
            {
                await context.Keyboard.FlushInputAsync().ConfigureAwait(false);

                await context.Output.WriteAsync(Prompt).ConfigureAwait(false);

                // TODO: Await keyboard input or cancellation, erase prompt and continue

                while (true)
                {
                    var keys = await context.Keyboard.ReadAsync(false).ConfigureAwait(false);

                    if (context.Input.IsEnded)
                    {
                        await ClearPromptAsync().ConfigureAwait(false);

                        return Action.Quit;
                    }

                    if (keys.Any() && keys.First().action == KeyAction.Character)
                    {
                        switch (char.ToLowerInvariant(keys.First().character))
                        {
                            case ' ':
                                await ClearPromptAsync().ConfigureAwait(false);
                                return Action.ShowPage;

                            case '\r':
                                await ClearPromptAsync().ConfigureAwait(false);
                                return Action.ShowLine;

                            case 'q':
                                await ClearPromptAsync().ConfigureAwait(false);
                                context.RequestCancel();
                                return Action.Quit;
                        }
                    }
                };
            }

            // Dummy return value
            return Action.ShowPage;
        }
    }
}
