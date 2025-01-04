# Creating custom commands based on the StdCommand class

[`StdCommand`](../src/SoftShell/StdCommand.cs) is a base class for commands that have a common way of defining and handling parameters, options and subcommands. Information for the Help command is automatically provided.

## Properties to override

The following properties must be overridden:

* `Name`: The name of the command, as the user must write to execute it.
* `Description`: The description of the command. This will be shown by the Help command.

## Construction

The constructor (parameterless or not) can do the following:

* Define parameters (general, i.e. for a non-subcommand, or for specific subcommands).
* Define options as flags or with values (general, i.e. for a non-subcommand, or for specific subcommands).
* Define subcommands, i.e. describe different behaviours of the command. Each subcommand can have its own parameters and options.
* Define a non-subcommand, i.e. describe behaviour of the command when no subcommand is used. If no subcommands are defined, the non-subcommand is implicitly defined.

### Parameters

Commands can have parameters that the user can or must provide arguments for on the command line, e.g.

`mycommand arg1 arg2`

Each parameter is defined with a name, a description, and a function delegate that gets an argument value from a user-provided argument string:

* The parameter name (of type `string`) is not case sensitive, and it cannot have whitespaces.
* The description (of type `string`) is used for the help command.
* The argument value function delegate (of type `Func<string, object>`) takes the user-provided argument string and returns a value object.
Examples:
  * `val => val` returns the argument as a string.
  * `val => int.Parse(val)` returns the argument as a parsed integer value (or throws an exception if invalid).

**Defining a required parameter:**  
Call the following method from the command constructor: `HasRequiredParameter(name, description, toObject)`

**Defining an optional parameter:**  
Call the following method from the command constructor: `HasOptionalParameter(name, description, toObject)`

### Options

Commands can have options, starting with a '/' or '-' character, that the user can or must provide on the command line, e.g.

`mycommand /option1=value1 /option2 -option3=value3 -option4`

Options can use values or just be considered as flags.

Each option is defined with a name, a description, and optionally a function delegate that gets a value from a user-provided option string:

* The option name (of type `string`) is not case sensitive, and it cannot have whitespaces.  
*(The name does not include the leading '/' or '-' character.)*
* The description (of type `string`) is used for the help command.
* If the option uses a value:  
The option value function delegate (of type `Func<string, object>`) takes the user-provided option value string and returns a value object.  
Examples:
  * `val => val` returns the value as a string.
  * `val => int.Parse(val)` returns the value as a parsed integer value (or throws an exception if invalid).

**Defining a required value option:**  
Call the following method from the command constructor: `HasRequiredValueOption(name, description, toObject)`

**Defining an optional value option:**  
Call the following method from the command constructor: `HasValueOption(name, description, toObject)`

**Defining an optional flag option:**  
Call the following method from the command constructor: `HasFlagOption(name, description)`

### Subcommands

Commands can have subcommands for different behaviour, e.g.

`mycommand add`  
`mycommand remove`

Each subcommand is defined with a name and a description:

* The subcommand name (of type `string`) is not case sensitive, and it cannot have whitespaces.
* The description (of type `string`) is used for the help command.

**Defining a subcommand:**  
Call the following method from the command constructor: `HasSubcommand(name, description)`

Parameters and options are defined for a subcommand as the following example illustrates:

    HasSubcommand("add", "Adds a key/value item.")
      .HasRequiredParameter("key", "Key of the item.", val => val)
      .HasRequiredParameter("value", "Value of the item.", val => val)
      .HasFlagOption("overwrite", "Overwrite if existing instead of failing.");

## Execution

Each time the command is executed the following method is invoked (must be overridden in your custom command):

`Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)`

where  
* `subcommand`, `args` and `options` are the command-line information provided by the user.
* `context` is an [`IStdCommandExecutionContext`](../src/SoftShell/IStdCommandExecutionContext.cs) and [`ICommandExecutionContext`](../src/SoftShell/ICommandExecutionContext.cs) object that provides more info about the command invokation, the session and the command input/output. Contains a command cancellation token that should be observed.
* `commandLine` is the raw command line that can be checked if needed. Note that environment variable expansion is done before passing this argument.

