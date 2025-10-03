# Phase 6: Rollback and Risk Mitigation Plan

**Parent Plan**: [00-HANGFIRE-DI-REFACTORING.md](../00-HANGFIRE-DI-REFACTORING.md)

## Objective

Define clear rollback procedures and risk mitigation strategies for the Hangfire DI refactoring.

## Rollback Scenarios

### 6.1 Immediate Rollback (Critical Failure)

**Trigger Conditions**:
- Production deployment causes service outage
- Hangfire Dashboard completely broken
- Background jobs stop executing
- Memory leak detected

**Rollback Steps**:

```bash
# Step 1: Revert all changes
git revert HEAD~1  # If single commit
# OR
git checkout <last-known-good-commit>

# Step 2: Rebuild and redeploy
dotnet clean
dotnet build
dotnet publish

# Step 3: Verify rollback
dotnet test --filter "FullyQualifiedName~Smoke"
```

### 6.2 Partial Rollback (Test Issues Only)

**Trigger Conditions**:
- Tests failing but production working
- Specific test collection has issues
- Performance degradation in tests only

**Selective Rollback**:

```bash
# Revert only test changes
git checkout HEAD~1 -- src/Orchestra.Tests/TestWebApplicationFactory.cs
git checkout HEAD~1 -- src/Orchestra.Tests/RealEndToEndTestFactory.cs
git checkout HEAD~1 -- src/Orchestra.Tests/Integration/IntegrationTestBase.cs

# Keep production changes
# Leave Startup.cs and core services intact
```

### 6.3 Configuration Rollback

**If DI configuration causes issues**:

```csharp
// In Startup.cs - Add feature flag
var useLegacyHangfireStorage = configuration.GetValue<bool>("UseLegacyHangfireStorage", false);

if (useLegacyHangfireStorage)
{
    // Legacy approach - rely on JobStorage.Current
    // Don't register JobStorage or IHangfireStorageService
}
else
{
    // New DI approach
    services.AddSingleton<JobStorage>(provider => JobStorage.Current);
    services.AddSingleton<IHangfireStorageService, HangfireStorageService>();
}
```

## Risk Mitigation Strategies

### 7.1A: Pre-Deployment Validation

```powershell
# Pre-deployment checklist script
param([string]$Environment = "Staging")

Write-Host "Running pre-deployment validation for $Environment" -ForegroundColor Cyan

# Check 1: All tests passing
$testResult = dotnet test --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed - deployment blocked"
    exit 1
}

# Check 2: No disposal errors
$logs = dotnet test --no-build 2>&1
if ($logs -match "ObjectDisposedException") {
    Write-Error "Disposal errors detected - deployment blocked"
    exit 1
}

# Check 3: Dashboard accessible
$dashboardUrl = "https://$Environment/hangfire"
try {
    $response = Invoke-WebRequest -Uri $dashboardUrl -UseBasicParsing
    if ($response.StatusCode -ne 200) {
        Write-Warning "Dashboard may have issues"
    }
} catch {
    Write-Warning "Could not verify dashboard"
}

Write-Host "Validation passed - safe to deploy" -ForegroundColor Green
```

### 7.1B: Gradual Rollout Strategy

**Phase 1: Development Environment**
- Deploy to dev
- Monitor for 24 hours
- Run automated tests

**Phase 2: Staging Environment**
- Deploy to staging
- Run load tests
- Monitor metrics

**Phase 3: Production Canary**
- Deploy to 10% of production
- Monitor error rates
- Check performance metrics

**Phase 4: Full Production**
- Complete rollout
- Continue monitoring

### 7.2A: Monitoring and Alerts

```csharp
// Add health check for Hangfire storage
public class HangfireStorageHealthCheck : IHealthCheck
{
    private readonly IHangfireStorageService _storageService;

    public HangfireStorageHealthCheck(IHangfireStorageService storageService)
    {
        _storageService = storageService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var api = _storageService.GetMonitoringApi();
            var stats = api.GetStatistics();

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Hangfire storage healthy. Queued: {stats.Queued}, Processing: {stats.Processing}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Hangfire storage unhealthy", ex));
        }
    }
}

// Register in Startup.cs
services.AddHealthChecks()
    .AddCheck<HangfireStorageHealthCheck>("hangfire-storage");
```

### 7.2B: Logging Enhancement

```csharp
// Add detailed logging to track DI usage
public class HangfireStorageService : IHangfireStorageService
{
    private readonly ILogger<HangfireStorageService> _logger;

    public HangfireStorageService(JobStorage storage, ILogger<HangfireStorageService> logger)
    {
        _storage = storage;
        _logger = logger;
        _logger.LogInformation("HangfireStorageService initialized with {StorageType}",
            storage.GetType().Name);
    }

    public IMonitoringApi GetMonitoringApi()
    {
        _logger.LogDebug("GetMonitoringApi called");
        return _storage.GetMonitoringApi();
    }
}
```

## Recovery Procedures

### 8.1 If Tests Still Fail After Rollback

```bash
# Clean all test artifacts
rm -rf test-orchestra-*.db
rm -rf bin/
rm -rf obj/

# Rebuild everything
dotnet clean
dotnet restore
dotnet build

# Run tests with detailed logging
dotnet test --logger "console;verbosity=detailed" --blame
```

### 8.2 If Production Issues Persist

1. Check JobStorage.Current initialization
2. Verify Hangfire package versions
3. Review startup logs for exceptions
4. Check database connections
5. Verify background service registrations

## Documentation Updates

### 9.1 Update Runbooks
- Add new troubleshooting section
- Document DI configuration
- Include rollback procedures

### 9.2 Update Architecture Docs
- Explain DI pattern for Hangfire
- Document test isolation strategy
- Add diagrams showing new flow

## Success Verification After Rollback

```powershell
# Verify rollback success
$checks = @{
    "Tests Pass" = { (dotnet test --no-build) -and ($LASTEXITCODE -eq 0) }
    "No Disposal Errors" = {
        $output = dotnet test --no-build 2>&1
        -not ($output -match "ObjectDisposedException")
    }
    "Dashboard Works" = {
        try {
            Invoke-WebRequest "http://localhost:5000/hangfire" -UseBasicParsing
            $true
        } catch { $false }
    }
}

foreach ($check in $checks.GetEnumerator()) {
    $result = & $check.Value
    $status = if ($result) { "PASS" } else { "FAIL" }
    Write-Host "$($check.Key): $status" -ForegroundColor $(if ($result) { "Green" } else { "Red" })
}
```

## Acceptance Criteria

- [ ] Rollback procedures documented and tested
- [ ] Feature flag implemented for gradual rollout
- [ ] Health checks added for monitoring
- [ ] Logging enhanced for troubleshooting
- [ ] Recovery procedures validated
- [ ] Documentation updated

## Estimated Time: 30 minutes

## Notes

- Always backup before making changes
- Test rollback procedures in staging first
- Keep communication channels open during deployment
- Document any issues encountered for future reference