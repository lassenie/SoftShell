using System.Net;
using System.Reflection;

using ConsoleDemo1;
using ConsoleDemo1.Commands;

using SoftShell;
using SoftShell.Console;
using SoftShell.Telnet;

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

            // Allow both the console and Telnet terminals to connect.
            // 2323 is a common alternative to the standard Telnet port 23; being above 1024
            // it doesn't require elevated privileges to bind. Connect with: telnet localhost 2323
            const int TelnetPort = 2323;

            using (var consoleListener = shellHost.AddTerminalListener(ConsoleTerminalListener.Instance))
            using (var telnetListener = shellHost.AddTerminalListener(new TelnetTerminalListener(IPAddress.Loopback, TelnetPort)))
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