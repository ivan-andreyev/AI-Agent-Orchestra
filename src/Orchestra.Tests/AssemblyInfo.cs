using Xunit;

// NOTE: Test parallelization remains ENABLED (default xUnit behavior)
// Collections can run in parallel, tests within collections can run in parallel
// DiagnoseHangfireExecution has graceful error handling for disposed storage
// Tests do NOT depend on diagnostics - they verify actual file operations
// This allows maximum test performance while handling race conditions gracefully
