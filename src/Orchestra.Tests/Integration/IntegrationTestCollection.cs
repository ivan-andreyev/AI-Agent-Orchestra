using Xunit;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Test collection definition to ensure integration tests run sequentially
/// rather than in parallel to avoid database and Hangfire job conflicts.
///
/// NOTE: Each test class now gets its own database via IClassFixture<TestWebApplicationFactory<Program>>.
/// This collection only enforces sequential execution order, not shared state.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] for sequential test execution.
    // Database isolation is handled at the class level via IClassFixture.
}