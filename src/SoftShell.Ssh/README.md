# SoftShell.Ssh

![Logo](https://raw.githubusercontent.com/lassenie/SoftShell/d756597f7a28654653d51723fe49dfe08af53796/doc/graphics/Logo.png)

[![NuGet version](https://img.shields.io/nuget/v/SoftShell.Ssh)](https://www.nuget.org/packages/SoftShell.Ssh)
[![NuGet downloads](https://img.shields.io/nuget/dt/SoftShell.Ssh)](https://www.nuget.org/packages/SoftShell.Ssh)
[![License: MIT](https://img.shields.io/github/license/lassenie/SoftShell)](https://github.com/lassenie/SoftShell/blob/master/LICENSE)

This free .NET 8 library adds an encrypted SSH terminal interface to [SoftShell](https://github.com/lassenie/SoftShell/blob/master/src/SoftShell/README.md) - a built-in command shell in your application for various monitoring or manipulation tasks.

It requires the core `SoftShell` package, which provides the shell host and core commands.

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

    // Support SSH terminals. The listener loads or creates its RSA host key in the
    // given directory, keeping the server's identity stable across restarts.
    using (var sshListener = shellHost.AddTerminalListener(
                    new SshTerminalListener(IPAddress.Loopback, 2222, AppContext.BaseDirectory))) // localhost port 2222 as example (2222 needs no elevated privileges)
    {
        // While your application runs...
    }
}
```

See the ConsoleDemoSsh application in the solution for further details.

## Authentication

User authentication is handled by SoftShell, not by the SSH layer. The connection is encrypted, but any SSH credentials are accepted; access control is performed by SoftShell through the interactive login on the terminal.

## Creating custom commands

Besides the core commands that come with SoftShell you can create your own custom commands.

[Read more](https://github.com/lassenie/SoftShell/blob/master/doc/CreatingCustomCommands.md)

## Contribution

Any help developing this library is welcomed.

Ideas for additions and improvements:
- General improvements
- Documentation and demo projects
- Unit tests
- Publicity (blogs posts etc.)

## Feature requests and bug reports

If you discover any bugs or have suggestions for improvements, please report them [here](https://github.com/lassenie/SoftShell/issues).

## Disclaimer

The library is under MIT License and provided as-is without any kind of warranty. See the LICENSE file for conditions.
