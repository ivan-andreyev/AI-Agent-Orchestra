using Xunit;

// PHASE 1 FIX: Disable test parallelization to break RealE2E ↔ Integration cycle
// ROOT CAUSE: HangfireServer is a global singleton that cannot serve two different
// JobStorage instances simultaneously. When Integration and RealE2E collections run
// in parallel, they share the same HangfireServer but have isolated storage, causing
// "Cannot access disposed SQLiteStorage" errors and cyclic test failures.
//
// SOLUTION: Run collections sequentially until Phase 2 architectural fix
// (removing HangfireServer from tests entirely for synchronous execution).
//
// This setting ensures:
// - Integration collection completes fully before RealE2E starts
// - No shared HangfireServer conflicts
// - Stable 582/582 test results
// - Trade-off: Slower execution (~6-8 minutes vs 4-5 minutes)
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
