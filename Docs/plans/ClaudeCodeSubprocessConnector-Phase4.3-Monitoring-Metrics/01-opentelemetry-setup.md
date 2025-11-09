# Task 1: OpenTelemetry Setup and Configuration

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4.3-Monitoring-Metrics.md](../ClaudeCodeSubprocessConnector-Phase4.3-Monitoring-Metrics.md)
**Estimate**: 30 minutes
**Priority**: P0 (Foundation for all metrics)

## Overview

Install and configure OpenTelemetry.NET packages with Prometheus exporter. This provides the foundation for all metric collection in the escalation system.

## Detailed Implementation Steps

### 1.1A: Install NuGet Packages (10 minutes)

#### Step 1: Update Orchestra.API.csproj
**File**: src/Orchestra.API/Orchestra.API.csproj
**Tool Calls**: 2

```xml
<PackageReference Include="OpenTelemetry" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.10.0-rc.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-rc.1" />
```

#### Step 2: Update Orchestra.Core.csproj
**File**: src/Orchestra.Core/Orchestra.Core.csproj
**Tool Calls**: 1

```xml
<PackageReference Include="OpenTelemetry.Api" Version="1.10.0" />
```

#### Step 3: Restore and Verify
**Tool Calls**: 2

```bash
dotnet restore src/Orchestra.API/Orchestra.API.csproj
dotnet restore src/Orchestra.Core/Orchestra.Core.csproj
```

### 1.1B: Create OpenTelemetryOptions Configuration (10 minutes)

#### Step 1: Create Options Class
**File**: src/Orchestra.Core/Options/OpenTelemetryOptions.cs
**Tool Calls**: 3

```csharp
using System.ComponentModel.DataAnnotations;

namespace Orchestra.Core.Options
{
    /// <summary>
    /// Конфигурация OpenTelemetry для метрик и трассировки
    /// </summary>
    public class OpenTelemetryOptions
    {
        public const string SectionName = "OpenTelemetry";

        /// <summary>
        /// Включить OpenTelemetry
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Имя сервиса для идентификации в метриках
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string ServiceName { get; set; } = "Orchestra.API";

        /// <summary>
        /// Версия сервиса (автоматически из сборки если не указано)
        /// </summary>
        public string? ServiceVersion { get; set; }

        /// <summary>
        /// Включить сбор метрик
        /// </summary>
        public bool MetricsEnabled { get; set; } = true;

        /// <summary>
        /// Включить трассировку (отключено по умолчанию)
        /// </summary>
        public bool TracingEnabled { get; set; } = false;

        /// <summary>
        /// Путь для Prometheus scraping endpoint
        /// </summary>
        [Required]
        [RegularExpression(@"^/[a-zA-Z0-9\-_/]*$")]
        public string PrometheusEndpoint { get; set; } = "/metrics";

        /// <summary>
        /// Включить метрики ASP.NET Core
        /// </summary>
        public bool AspNetCoreInstrumentationEnabled { get; set; } = true;

        /// <summary>
        /// Включить метрики HTTP клиентов
        /// </summary>
        public bool HttpClientInstrumentationEnabled { get; set; } = true;

        /// <summary>
        /// Включить метрики Entity Framework
        /// </summary>
        public bool EntityFrameworkInstrumentationEnabled { get; set; } = true;

        /// <summary>
        /// Интервал экспорта метрик в секундах
        /// </summary>
        [Range(1, 300)]
        public int ExportIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Максимальное количество метрик в памяти
        /// </summary>
        [Range(100, 100000)]
        public int MaxMetricPoints { get; set; } = 10000;

        /// <summary>
        /// Включить детальные метрики (может повлиять на производительность)
        /// </summary>
        public bool DetailedMetricsEnabled { get; set; } = false;
    }
}
```

#### Step 2: Register Configuration Validation
**File**: src/Orchestra.API/Program.cs (modification)
**Tool Calls**: 1

```csharp
// Add to ConfigureServices section
builder.Services.AddOptions<OpenTelemetryOptions>()
    .BindConfiguration(OpenTelemetryOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### 1.1C: Configure in appsettings.json (10 minutes)

#### Step 1: Add to appsettings.json
**File**: src/Orchestra.API/appsettings.json
**Tool Calls**: 2

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "ServiceName": "Orchestra.API",
    "ServiceVersion": null,
    "MetricsEnabled": true,
    "TracingEnabled": false,
    "PrometheusEndpoint": "/metrics",
    "AspNetCoreInstrumentationEnabled": true,
    "HttpClientInstrumentationEnabled": true,
    "EntityFrameworkInstrumentationEnabled": true,
    "ExportIntervalSeconds": 60,
    "MaxMetricPoints": 10000,
    "DetailedMetricsEnabled": false
  }
}
```

#### Step 2: Add Development Overrides
**File**: src/Orchestra.API/appsettings.Development.json
**Tool Calls**: 1

```json
{
  "OpenTelemetry": {
    "DetailedMetricsEnabled": true,
    "ExportIntervalSeconds": 10
  }
}
```

#### Step 3: Add Production Overrides
**File**: src/Orchestra.API/appsettings.Production.json
**Tool Calls**: 1

```json
{
  "OpenTelemetry": {
    "DetailedMetricsEnabled": false,
    "ExportIntervalSeconds": 60,
    "MaxMetricPoints": 50000
  }
}
```

## Integration Points

### Program.cs Configuration
**Location**: After services configuration, before app.Build()
**Tool Calls**: 3

```csharp
// Configure OpenTelemetry
var openTelemetryOptions = builder.Configuration
    .GetSection(OpenTelemetryOptions.SectionName)
    .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

if (openTelemetryOptions.Enabled)
{
    var assemblyVersion = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "1.0.0";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: openTelemetryOptions.ServiceName,
                serviceVersion: openTelemetryOptions.ServiceVersion ?? assemblyVersion))
        .WithMetrics(metrics =>
        {
            if (openTelemetryOptions.MetricsEnabled)
            {
                metrics.AddMeter("Orchestra.*");

                if (openTelemetryOptions.AspNetCoreInstrumentationEnabled)
                    metrics.AddAspNetCoreInstrumentation();

                if (openTelemetryOptions.HttpClientInstrumentationEnabled)
                    metrics.AddHttpClientInstrumentation();

                if (openTelemetryOptions.EntityFrameworkInstrumentationEnabled)
                    metrics.AddEntityFrameworkCoreInstrumentation();

                metrics.AddPrometheusExporter();
            }
        });
}

// After app.Build()
if (openTelemetryOptions.Enabled && openTelemetryOptions.MetricsEnabled)
{
    app.UseOpenTelemetryPrometheusScrapingEndpoint(
        openTelemetryOptions.PrometheusEndpoint);
}
```

## Validation Checklist

### Package Installation
- [ ] All 5 OpenTelemetry packages installed
- [ ] No version conflicts reported
- [ ] dotnet restore successful

### Configuration
- [ ] OpenTelemetryOptions class compiles
- [ ] Options validation attributes working
- [ ] Configuration loads from appsettings.json

### Integration
- [ ] Program.cs modifications compile
- [ ] Service starts without errors
- [ ] /metrics endpoint accessible

## Common Issues and Solutions

### Issue 1: Package Version Conflicts
**Solution**: Use consistent versions across all OpenTelemetry packages

### Issue 2: Prometheus Endpoint Not Found
**Solution**: Ensure UseOpenTelemetryPrometheusScrapingEndpoint is called after app.Build()

### Issue 3: No Metrics Appearing
**Solution**: Verify meter names match pattern in AddMeter("Orchestra.*")

## Test Verification

### Manual Testing
1. Start the application
2. Navigate to http://localhost:5000/metrics
3. Verify Prometheus format output appears
4. Look for default ASP.NET Core metrics

### Automated Test
```csharp
[Fact]
public async Task MetricsEndpoint_ReturnsPrometheusFormat()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act
    var response = await client.GetAsync("/metrics");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().Contain("# TYPE");
    content.Should().Contain("# HELP");
}
```

## Next Steps

Once OpenTelemetry setup is complete:
1. Proceed to Task 2: Create MetricsProvider base infrastructure
2. Verify metrics endpoint is accessible
3. Test with Prometheus scraper if available

## Success Criteria

- ✅ All OpenTelemetry packages installed without conflicts
- ✅ Configuration loaded successfully via IOptions
- ✅ /metrics endpoint returns Prometheus format
- ✅ No runtime errors on application startup
- ✅ Environment-specific settings working correctly