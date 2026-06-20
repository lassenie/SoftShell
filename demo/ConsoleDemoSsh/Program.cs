using System.Net;
using System.Reflection;

using ConsoleDemoSsh;
using ConsoleDemoSsh.Commands;

using FxSsh;

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

    // Allow the SSH terminals to connect
    const int CshPort = 22;

    using (var sshListener = new SshTerminalListener(IPAddress.Loopback, CshPort))
    {
        // Add host key(s) before adding the listener to the SoftShell host.
        // The host key is persisted to disk so it stays the same across restarts;
        // otherwise connecting clients would warn that the server's host key changed.
        sshListener.KeyRsa256 = LoadOrCreateRsaHostKey();

        shellHost.AddTerminalListener(sshListener);

        Console.WriteLine($"Accepting SSH connections on port {CshPort}...");

        // Wait for a command to exit the whole application (a real application would do something meaningful)
        while (!exitCode.HasValue)
            Thread.Sleep(1000);
    }
}

// Return the exit code given through the SoftShell command that ends the application
return exitCode ?? 0;

// Loads the RSA host key from disk, generating and persisting a new one on first run.
// A persistent host key lets clients recognize the server across restarts and is what
// protects the (already encrypted) connection against man-in-the-middle attacks.
static string LoadOrCreateRsaHostKey()
{
    var keyFilePath = Path.Combine(AppContext.BaseDirectory, "ssh_host_rsa.pem");

    if (File.Exists(keyFilePath))
        return File.ReadAllText(keyFilePath);

    var pem = KeyGenerator.GenerateRsaKeyPem(2048);
    File.WriteAllText(keyFilePath, pem);

    return pem;
}
