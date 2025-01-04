using ConsoleDemo1;
using ConsoleDemo1.Commands;
using SoftShell;
using SoftShell.Console;
using SoftShell.Telnet;
using System.Net;
using System.Reflection;

internal class Program
{
    private static int Main(string[] args)
    {
        int? exitCode = null;

        // Create the SoftShell host with core commands
        // that will run terminal sessions until the application is terminated
        using (var shellHost = new SoftShellHost(new AppUserAuthentication())) // or new SoftShellHost(NoUserValidation.Create()))
        {
            // Add application-specific commands needing special construction
            shellHost.AddCommand("app", "ConsoleDemo1", new AppExitCommand(code => exitCode = code));

            // Add remaining application-specific commands assumed to have default constructors
            shellHost.AddCommands("app", "ConsoleDemo1", Assembly.GetExecutingAssembly());

            // Allow both the console and Telnet terminals to connect
            using (var consoleListener = shellHost.AddTerminalListener(ConsoleTerminalListener.Instance))
            using (var telnetListener = shellHost.AddTerminalListener(new TelnetTerminalListener(IPAddress.Loopback, 23)))
            {
                // Wait for a command to exit the whole application (a real application would do something meaningful)
                while (!exitCode.HasValue)
                    Thread.Sleep(1000);
            }
        }

        // Return the exit code given through the SoftShell command that ends the application
        return exitCode ?? 0;
    }
}