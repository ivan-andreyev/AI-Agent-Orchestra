using Xunit;

namespace Orchestra.Tests.Integration;

/// <summary>
/// Test collection definition to ensure integration tests run sequentially
/// rather than in parallel to avoid database and Hangfire job conflicts
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory<Program>>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}