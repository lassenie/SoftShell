# SoftShell

![Logo](https://raw.githubusercontent.com/lassenie/SoftShell/d756597f7a28654653d51723fe49dfe08af53796/doc/graphics/Logo.png)

This free .NET library provides SSH terminal support for SoftShell - a built-in command shell in your application for various monitoring or manipulation tasks.

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

    // Support SSH terminals
    using (var sshListener = shellHost.AddTerminalListener(
                    new SshTerminalListener(IPAddress.Loopback, 22))) // localhost port 22 as example
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
- New terminal interfaces (e.g. web, SSH)
- General improvements
- Documentation and demo projects
- Unit tests
- Publicity (blogs posts etc.)

## Feature requests and bug reports

If you discover any bugs or have suggestions for improvements, please report them [here](https://github.com/lassenie/SoftShell/issues).

## Disclaimer

The library is under MIT License and provided as-is without any kind of warranty. See the LICENSE file for conditions.
