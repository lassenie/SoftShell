# Creating custom commands based on the Command class

[`Command`](../src/SoftShell/Command.cs) is the base class for all commands, including `StdCommand`. It can be used directly as base class when custom ways of parsing the command line is needed. Information for the Help command must be provided manually.

## Properties to override

The following properties must be overridden:

* `Name`: The name of the command, as the user must write to execute it.
* `Description`: The description of the command. This will be shown by the Help command.

## Construction

There are no special requirements to construction of the command.

## Execution

Each time the command is executed the following method is invoked (must be overridden in your custom command):

`Task ExecuteAsync(ICommandExecutionContext context, string commandLine, IEnumerable<CommandLineToken> tokens)`

where  
* `context` is an object that provides more info about the command invokation, the session and the command input/output. Contains a command cancellation token that should be handled.
* `commandLine` is the command line provided by the user. Note that environment variable expansion is done before passing this argument.
* `tokens` is a suggested list of tokens parsed from the command line by SoftShell.

## Help information

When the user executes the Help command to get detailed about your custom command, the following method is invoked (must be overridden in your custom command):

`string GetHelpText(ICommandExecutionContext context, string subcommandName)`

where  
* `context` is an [`ICommandExecutionContext`](../src/SoftShell/ICommandExecutionContext.cs) object that provides more info about the Help command invokation, the session and the command input/output. Contains a command cancellation token that should be observed.
* `subcommandName` is an optional name of a subcommand that the user requests help information for. Only relevant if you implement some sort of subcommands in your custom command.
You should throw an exception if an unsupported subcommand argument is given.

Note: It may be relevant to have the help text formatted according to the terminal's window width. This information can be obtained through the `context.Output.WindowWidth` property (null if unavailable).
