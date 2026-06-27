# SoftShell

![Logo](https://raw.githubusercontent.com/lassenie/SoftShell/d756597f7a28654653d51723fe49dfe08af53796/doc/graphics/Logo.png)

[![NuGet version](https://img.shields.io/nuget/v/SoftShell)](https://www.nuget.org/packages/SoftShell)
[![NuGet downloads](https://img.shields.io/nuget/dt/SoftShell)](https://www.nuget.org/packages/SoftShell)
[![License: MIT](https://img.shields.io/github/license/lassenie/SoftShell)](https://github.com/lassenie/SoftShell/blob/master/LICENSE)

This free .NET Standard 2.0 library provides a built-in command shell in your application for various monitoring or manipulation tasks.

Through a terminal interface, such as the console or Telnet (unencrypted!), it is possible to log in and get a shell-like experience with login, command prompt and various commands that can be issued.

Standard commands, such as 'help' and 'exit' exist and more will probably come. Each application can add custom commands or terminal interfaces.

> For encrypted access over SSH, see the companion [`SoftShell.Ssh`](https://github.com/lassenie/SoftShell/blob/master/src/SoftShell.Ssh/README.md) package.

## Usage

When the user has signed in, a `>` command prompt is shown. Commands can then be entered as in the following examples:

```text
help
asm|more
env > variables.txt
exit
```

[Read more](https://github.com/lassenie/SoftShell/blob/master/doc/Usage.md)

## Integrating in your app

```csharp
// Create the SoftShell host with core commands
using (var shellHost = new SoftShellHost(UserAuthentication.None)) // or create your own user authentication class
{
    // Add your custom commands needing special construction
    shellHost.AddCommand("myapp", "My Application", new MyCustomCommand1(someArgs));
    shellHost.AddCommand("myapp", "My Application", new MyCustomCommand2(someArgs));
    ...

    // Add your remaining custom commands having default constructors
    shellHost.AddCommands("myapp", "My Application", Assembly.GetExecutingAssembly());

    // Support both the console and Telnet terminals
    using (var consoleListener = shellHost.AddTerminalListener(ConsoleTerminalListener.Instance))
    using (var telnetListener = shellHost.AddTerminalListener(
                    new TelnetTerminalListener(IPAddress.Loopback, 2323))) // localhost port 2323 as example (2323 needs no elevated privileges)
    {
        // While your application runs...
    }
}
```

See the ConsoleDemo1 application in the solution for further details.

## Creating custom commands

Besides the core commands that come with SoftShell you can create your own custom commands.

[Read more](https://github.com/lassenie/SoftShell/blob/master/doc/CreatingCustomCommands.md)

## Contribution

Any help developing this library is welcomed.

Ideas for additions and improvements:
- Additional commands
- New terminal interfaces (e.g. web)
- General improvements
- Documentation and demo projects
- Unit tests
- Publicity (blogs posts etc.)

## Feature requests and bug reports

If you discover any bugs or have suggestions for improvements, please report them [here](https://github.com/lassenie/SoftShell/issues).

## Disclaimer

The library is under MIT License and provided as-is without any kind of warranty. See the LICENSE file for conditions.
