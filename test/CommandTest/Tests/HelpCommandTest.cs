using SoftShell;
using SoftShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandTest.Tests
{
    public class HelpCommandTest : TestBase
    {
        private class Provider : HelpCommand.ICommandCollectionProvider
        {
            private readonly IEnumerable<Command> _commands;
            public Provider(IEnumerable<Command> commands) => _commands = commands;
            public IEnumerable<Command> GetCommands() => _commands;
        }

        private static T InGroup<T>(T command, CommandGroup group) where T : Command
        {
            command.Group = group;
            return command;
        }

        [Fact]
        public void TestUnknownCommand()
        {
            var provider = new Provider(Array.Empty<Command>());

            TestCommandWriteLine(new HelpCommand(provider), "help nope",
                                 "Unknown command: nope");
        }

        [Fact]
        public void TestGeneralHelp()
        {
            var general = new CommandGroup(string.Empty, "General", isCore: true);
            var provider = new Provider(new Command[]
            {
                InGroup(new ExitCommand(), general),
                InGroup(new ClearScreenCommand(), general)
            });

            var lines = RunAndCaptureLines(new HelpCommand(provider), "help");

            Assert.Equal(new[]
            {
                "General commands:",
                "cls   Clears the terminal's screen.",
                "exit  Terminates the SoftShell session (not the application).",
                "",
                "Type 'help <command-name>' to get detailed help for a command."
            }, lines);
        }

        [Fact]
        public void TestGroupsList()
        {
            var general = new CommandGroup(string.Empty, "General", isCore: true);
            var application = new CommandGroup("app", "Application", isCore: false);

            var provider = new Provider(new Command[]
            {
                InGroup(new ClearScreenCommand(), general),
                InGroup(new ExitCommand(), application)
            });

            var lines = RunAndCaptureLines(new HelpCommand(provider), "help -groups");

            Assert.Equal(new[]
            {
                "Name         Prefix",
                "----         ------",
                "Application  app",
                "General      "
            }, lines);
        }
    }
}
