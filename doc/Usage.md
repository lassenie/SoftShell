# SoftShell usage

When the user has signed in, a `>` command prompt is shown, and commands can then be entered.

## Help information

Listing available commands:

    help

Showing detailed help for a specific command and subcommand:

    help gc
    help gc disable

## Arguments and options

Arguments can be required or optional (see help information for each command) and can be given with or without quotes:

    asm tree SoftShell
    more "some file.txt"

Options can be given with either of the characters '/' or '-', directly followed by the option name.
Options can just be flags, or they can have values (see help information for each command).
Values are provided with an equal sign ('='), followed by the value (no spaces before/after the equal sign).
Option values can be given with or without quotes.
The order of the options is insignificant.

    gc disable -totalsize=20m
    help -groups
    grep -group="My Application"

## Piping

Commands can be piped by separating them with the '|' character:

    asm|more

Standard output from the first command will be piped as standard input to the next.

## Redirecting

Redirecting standard output and standard error:

    env > variables.txt
    tee 2> errors.txt

Redirecting both standard output and standard error to the same file:

    gc enable > alloutput.txt 2>&1
    gc enable 2> alloutput.txt > &2

Redirecting standard input:

    grep < inputfile.txt

## Environment variables

The environment variables for the application can be used in commands (not case sensitive).
Variables must be enclosed with '%' characters, e.g. `%username%`:

    env > %TEMP%/env.txt

The percent character can be escaped using `%%`.
