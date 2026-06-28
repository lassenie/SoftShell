# SoftShell

![Logo](https://raw.githubusercontent.com/lassenie/SoftShell/d756597f7a28654653d51723fe49dfe08af53796/doc/graphics/Logo.png)

[![Tests](https://github.com/lassenie/SoftShell/actions/workflows/tests.yml/badge.svg)](https://github.com/lassenie/SoftShell/actions/workflows/tests.yml)
[![Publish NuGet packages](https://github.com/lassenie/SoftShell/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/lassenie/SoftShell/actions/workflows/publish-nuget.yml)
[![SoftShell on NuGet](https://img.shields.io/nuget/v/SoftShell?label=SoftShell)](https://www.nuget.org/packages/SoftShell)
[![SoftShell.Ssh on NuGet](https://img.shields.io/nuget/v/SoftShell.Ssh?label=SoftShell.Ssh)](https://www.nuget.org/packages/SoftShell.Ssh)
[![License: MIT](https://img.shields.io/github/license/lassenie/SoftShell)](LICENSE)

SoftShell is a free .NET library that provides a built-in command shell in your application for various monitoring or manipulation tasks.

Through a terminal interface it is possible to log in and get a shell-like experience with login, command prompt and various commands that can be issued. Standard commands, such as `help` and `exit`, exist and more will probably come. Each application can add custom commands or terminal interfaces.

When the user has signed in, a `>` command prompt is shown. Commands can then be entered as in the following examples:

```text
help
asm|more
env > variables.txt
exit
```

## Packages

| Package | Target framework | Description |
|---------|------------------|-------------|
| [**SoftShell**](src/SoftShell/README.md) | .NET Standard 2.0 | Core library with the shell host, core commands, and the console and Telnet (unencrypted!) terminal interfaces. |
| [**SoftShell.Ssh**](src/SoftShell.Ssh/README.md) | .NET 8 | Adds an encrypted SSH terminal interface to SoftShell. |

## Documentation

- [Usage](doc/Usage.md)
- [Creating custom commands](doc/CreatingCustomCommands.md)

## Demos

- `demo/ConsoleDemo1` — console and Telnet terminals.
- `demo/ConsoleDemoSsh` — SSH terminal.

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
