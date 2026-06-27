using System.Net;
using System.Reflection;

using ConsoleDemoSsh;
using ConsoleDemoSsh.Commands;

using SoftShell;
using SoftShell.Ssh;

int? exitCode = null;

// Create the SoftShell host with core commands
// that will run terminal sessions until the application is terminated
using (var shellHost = new SoftShellHost(new AppUserAuthentication())) // or new SoftShellHost(NoUserValidation.Create()))
{
    // Add application-specific commands needing special construction
    shellHost.AddCommand("app", "ConsoleDemoSsh", new AppExitCommand(code => exitCode = code));

    // Add remaining application-specific commands assumed to have default constructors
    shellHost.AddCommands("app", "ConsoleDemoSsh", Assembly.GetExecutingAssembly());

    // Allow the SSH terminals to connect.
    // 2222 is a common alternative to the standard SSH port 22; being above 1024 it
    // doesn't require elevated privileges to bind. Connect with: ssh -p 2222 localhost
    const int SshPort = 2222;

    // The listener loads or creates its RSA host key in the given directory, so it stays
    // the same across restarts; otherwise connecting clients would warn that the server's
    // host key changed.
    using (var sshListener = new SshTerminalListener(IPAddress.Loopback, SshPort, AppContext.BaseDirectory))
    {
        shellHost.AddTerminalListener(sshListener);

        Console.WriteLine($"Accepting SSH connections on port {SshPort}...");

        // Wait for a command to exit the whole application (a real application would do something meaningful)
        while (!exitCode.HasValue)
            Thread.Sleep(1000);
    }
}

// Return the exit code given through the SoftShell command that ends the application
return exitCode ?? 0;
