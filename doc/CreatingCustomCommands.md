# Creating custom commands

Besides the core commands that come with SoftShell you can create your own custom commands.

## Standard or basic commands

Each command is implemented as a class, which should be based on either the `StdCommand` class or the `Command` class.

* [`StdCommand`](../src/SoftShell/StdCommand.cs) is a base class for commands that have a common way of defining and handling parameters, options and subcommands. Information for the Help command is automatically provided.
* [`Command`](../src/SoftShell/Command.cs) is the base class for all commands, including `StdCommand`. It can be used directly as base class when custom ways of parsing the command line is needed. Information for the Help command must be provided manually.

[Creating custom commands based on the StdCommand class](CreatingCustomCommandsWithStdCommand.md)

[Creating custom commands based on the Command class](CreatingCustomCommandsWithCommand.md)

## Adding your custom commands at runtime

Commands are instantiated once when being added (made available) to the `SoftShellHost` object. A command object can then be executed a number of times - this happens when the `ExecuteAsync` method is called from an active SoftShell session, invoked by the user.

Since a command object may be executed more than once, it should not have state variables as members. Instead the `ExecuteAsync` method can create local variables that will run out of scope when execution ends.

As shown in the [main README file](/README.md) custom commands can be added in two ways, depending on each command class constructor:

* Each command having a constructor with parameters can be added by calling  
`SoftShellHost.AddCommand(string groupPrefix, string groupName, Command command)`.
* All commands with parameterless constructors defined in a given .NET assembly can be added with a single call to  
`AddCommands(string groupPrefix, string groupName, Assembly assembly)`.

Both methods require two common arguments:
* `groupPrefix` is a group prefix (like a namespace) for the command(s). The prefix should be without namespaces or periods (.). Exampe: **"myapp"**.
* `groupname` is a friendly name for the group that the command belongs to. It is mainly used when listing commands using the Help command. Example: **"My Application"**.

## Command groups

All commands are organised in groups. This way they are logically grouped, and they are unique even if commands from different groups have the same name (imagine a custom app command called "Info" which would clash with the core Info command).

If only one command with a given name exists, the user can simply execute it by entering the command name.

Core commands can be executed explicitly either by providing an empty group name, or the group name **"core"**. For example, both **core.help** and **.help** will execute the core Help command. If no custom command called "Help" exists, **help** will also execute the core Help command since that name is unique.

Custom commands can be executed explicitly by providing the group prefix. For example, **myapp.info** will execute the "My Application" Info command.

## Command input/output

Commands can read input in two ways:

* Standard input: The `ExecuteAsync` method is invoked with a context argument, which has an `Input` property. This property implements the [`ICommandInput`](../src/SoftShell/IO/ICommandInput.cs) interface with various properties and methods, e.g.:
  * `ReadLineAsync()`: Returns a line of text received from the user terminal or as redirected or piped input. The method is blocking until a terminated line of text is received, or throws an exception if the command is cancelled.
  * `ReadAsync()`: Returns one or more received characters from the user terminal or as redirected or piped input. The method is blocking until one or more characters are available, or throws an exception if the command is cancelled.
  * `TryReadAsync()`: Returns zero, one or more received characters from the user terminal or as redirected or piped input. The method never blocks but returns an empty string if no characters are available.
  * `IsEnded`: Returns true if the input is ended for the command, or the command execution is cancelled.
* Keyboard input: The `ExecuteAsync` method is invoked with a context argument, which has a `Keyboard` property. This property implements the [`IKeyboardInput`](../src/SoftShell/IO/IKeyboardInput.cs) interface which is similar to the `ICommandInput` interface. The interface is used in rare cases if a command specifically wants direct keyboard input rather than standard input.

Commands can write output in two ways:

* Standard output: The `ExecuteAsync` method is invoked with a context argument, which has an `Output` property. This property implements the [`ICommandOutput`](../src/SoftShell/IO/ICommandOutput.cs) interface with various properties and methods, e.g.:
  * `WriteLineAsync(text)`: Writes a line of text to the user terminal or as redirected or piped output.
  * `WriteAsync(text)`: Writes a text to the user terminal or as redirected or piped output.
  * `ClearScreenAsync()`: Clears the screen on the user terminal, unless the output is redirected or piped.
  * `WindowWidth` and `WindowHeight`: Gets the with/height, as number of characters, of the user terminal, or null if unknown.
  * `LineTermination`: Gets the string with character sequence for a line termination, used by the terminal.
* Standard error: The `ExecuteAsync` method is invoked with a context argument, which has an `ErrorOutput` property. This property implements the [`ICommandErrorOutput`](../src/SoftShell/IO/ICommandErrorOutput.cs) interface which is similar to the `ICommandOutput` interface. The interface is used for error information.
