// The console tests redirect the process-wide System.Console streams and the Telnet
// tests open real loopback sockets and spin up the terminal's background worker tasks.
// Neither is safe to run concurrently with the others, so run all tests serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
