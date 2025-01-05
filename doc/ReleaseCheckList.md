# Release check list

1. Update the following properties in the src/SoftShell/SoftShell.csproj file:
    * `Version`: The new package version, e.g. 1.0.0
    * `AssemblyVersion`: Assembly version, e.g. 1.0.0.0
    * `FileVersion`: File version, e.g. 1.0.0.0
    * `Copyright`: Copyright statement if needing update
    * `PackageReleaseNotes`: Describe the release
2. Update the LICENSE file if needed, e.g. the Copyright statement.
3. Run all unit tests and check that they all pass.
4. Manually test using the ConsoleDemo1 project:
    * Terminals: Console and Telnet
    * Login
    * Entering commands with parameters and options.
    * Piping
    * Redirects
3. Git commit and push.
4. Add the following Git tag and push: softshell/*major-version*/*minor-version*/v *package-version*, e.g. `softshell/1/0/v1.0.0`
5. Make a release build and check that a SoftShell...nupkg file is created in the src/SoftShell/bin/Release folder. The file name should have the correct package version in it.
6. Sign into nuget.org as lassenie.
7. Upload the new SoftShell...nupkg file, check that all information is correct and click Submit.

 
