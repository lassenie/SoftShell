// The execution tests drive a shared in-process shell through busy-wait loops
// (see TestBase) and are not safe to run concurrently: under xUnit's default
// parallel collection execution they non-deterministically deadlock (thread-pool
// starvation) or corrupt each other's terminal input. Run them serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
