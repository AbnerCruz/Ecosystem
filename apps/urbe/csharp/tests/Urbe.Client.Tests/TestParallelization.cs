// Every UI test of this assembly drives the one headless Avalonia session (HeadlessUiTests.Session). xUnit ran the test
// classes in parallel, so EditorTests, TouchIntentTests and LibraryOpenPerfTests had their windows and dispatcher jobs
// interleaved with HeadlessUiTests' on that session; on CI the test process now and then died natively (exit 139) in
// the middle of the run with no test failing. One test at a time: the suite takes the same time (≈2 min).
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
