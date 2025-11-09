# Task 2: MetricsProvider Base Infrastructure

**Parent Plan**: [ClaudeCodeSubprocessConnector-Phase4.3-Monitoring-Metrics.md](../ClaudeCodeSubprocessConnector-Phase4.3-Monitoring-Metrics.md)
**Estimate**: 45 minutes
**Priority**: P0 (Foundation for all custom metrics)

## Overview

Create a base infrastructure for metrics collection that provides a clean abstraction over OpenTelemetry's IMeterFactory. This allows for consistent metric creation, error handling, and testability across all services.

## Detailed Implementation Steps

### 2.1A: Define IMetricsProvider Interface (15 minutes)

#### Step 1: Create Interface Definition
**File**: src/Orchestra.Core/Services/Metrics/IMetricsProvider.cs
**Tool Calls**: 4

```csharp
using System.Diagnostics.Metrics;

namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// Интерфейс для провайдера метрик системы
    /// </summary>
    public interface IMetricsProvider
    {
        /// <summary>
        /// Версия инструментации для метрик
        /// </summary>
        string InstrumentationVersion { get; }

        /// <summary>
        /// Имя метрики для идентификации источника
        /// </summary>
        string MeterName { get; }

        /// <summary>
        /// Получить или создать метр для сбора метрик
        /// </summary>
        /// <param name="name">Имя метра</param>
        /// <returns>Экземпляр метра</returns>
        IMeter GetMeter(string name);

        /// <summary>
        /// Создать счётчик для подсчёта событий
        /// </summary>
        /// <typeparam name="T">Тип значения счётчика</typeparam>
        /// <param name="name">Имя метрики</param>
        /// <param name="unit">Единица измерения</param>
        /// <param name="description">Описание метрики</param>
        /// <returns>Счётчик</returns>
        Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
            where T : struct;

        /// <summary>
        /// Создать гейдж для отслеживания текущего значения
        /// </summary>
        /// <typeparam name="T">Тип значения гейджа</typeparam>
        /// <param name="name">Имя метрики</param>
        /// <param name="observeValue">Функция получения значения</param>
        /// <param name="unit">Единица измерения</param>
        /// <param name="description">Описание метрики</param>
        /// <returns>Наблюдаемый гейдж</returns>
        ObservableGauge<T> CreateGauge<T>(
            string name,
            Func<T> observeValue,
            string? unit = null,
            string? description = null) where T : struct;

        /// <summary>
        /// Создать гистограмму для отслеживания распределения значений
        /// </summary>
        /// <typeparam name="T">Тип значения гистограммы</typeparam>
        /// <param name="name">Имя метрики</param>
        /// <param name="unit">Единица измерения</param>
        /// <param name="description">Описание метрики</param>
        /// <param name="buckets">Границы бакетов для гистограммы</param>
        /// <returns>Гистограмма</returns>
        Histogram<T> CreateHistogram<T>(
            string name,
            string? unit = null,
            string? description = null,
            double[]? buckets = null) where T : struct;

        /// <summary>
        /// Создать UpDownCounter для значений, которые могут увеличиваться и уменьшаться
        /// </summary>
        UpDownCounter<T> CreateUpDownCounter<T>(
            string name,
            string? unit = null,
            string? description = null) where T : struct;

        /// <summary>
        /// Записать событие с тегами
        /// </summary>
        void RecordEvent(string eventName, Dictionary<string, object?>? tags = null);

        /// <summary>
        /// Проверить, включён ли сбор метрик
        /// </summary>
        bool IsEnabled { get; }
    }
}
```

#### Step 2: Create Metric Tags Helper
**File**: src/Orchestra.Core/Services/Metrics/MetricTags.cs
**Tool Calls**: 1

```csharp
namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// Стандартные теги для метрик
    /// </summary>
    public static class MetricTags
    {
        public const string Endpoint = "endpoint";
        public const string Method = "method";
        public const string StatusCode = "status_code";
        public const string ErrorType = "error_type";
        public const string OperationType = "operation_type";
        public const string SessionId = "session_id";
        public const string ApprovalId = "approval_id";
        public const string Result = "result";
        public const string Reason = "reason";

        /// <summary>
        /// Создать теги для HTTP операции
        /// </summary>
        public static KeyValuePair<string, object?>[] ForHttpOperation(
            string endpoint,
            string method,
            int statusCode)
        {
            return new[]
            {
                new KeyValuePair<string, object?>(Endpoint, endpoint),
                new KeyValuePair<string, object?>(Method, method),
                new KeyValuePair<string, object?>(StatusCode, statusCode)
            };
        }

        /// <summary>
        /// Создать теги для операции одобрения
        /// </summary>
        public static KeyValuePair<string, object?>[] ForApproval(
            string approvalId,
            string result,
            string? sessionId = null)
        {
            var tags = new List<KeyValuePair<string, object?>>
            {
                new(ApprovalId, approvalId),
                new(Result, result)
            };

            if (!string.IsNullOrEmpty(sessionId))
                tags.Add(new(SessionId, sessionId));

            return tags.ToArray();
        }
    }
}
```

### 2.2B: Implement MetricsProvider Base Class (20 minutes)

#### Step 1: Create Base Implementation
**File**: src/Orchestra.Core/Services/Metrics/MetricsProvider.cs
**Tool Calls**: 6

```csharp
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;

namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// Базовая реализация провайдера метрик
    /// </summary>
    public abstract class MetricsProvider : IMetricsProvider, IDisposable
    {
        private readonly IMeterFactory _meterFactory;
        private readonly ILogger _logger;
        private readonly OpenTelemetryOptions _options;
        private readonly Dictionary<string, Meter> _meters;
        private readonly object _metersLock = new();
        private bool _disposed;

        protected MetricsProvider(
            IMeterFactory meterFactory,
            IOptions<OpenTelemetryOptions> options,
            ILogger logger,
            string meterName)
        {
            _meterFactory = meterFactory ?? throw new ArgumentNullException(nameof(meterFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _meters = new Dictionary<string, Meter>();

            MeterName = meterName;
            InstrumentationVersion = GetInstrumentationVersion();
            IsEnabled = _options.Enabled && _options.MetricsEnabled;

            if (IsEnabled)
            {
                _logger.LogInformation(
                    "MetricsProvider initialized: {MeterName} v{Version}",
                    MeterName, InstrumentationVersion);
            }
        }

        public string InstrumentationVersion { get; }
        public string MeterName { get; }
        public bool IsEnabled { get; }

        public IMeter GetMeter(string name)
        {
            if (!IsEnabled)
                return new NoOpMeter();

            lock (_metersLock)
            {
                if (!_meters.TryGetValue(name, out var meter))
                {
                    try
                    {
                        meter = _meterFactory.Create(name, InstrumentationVersion);
                        _meters[name] = meter;
                        _logger.LogDebug("Created meter: {MeterName}", name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create meter: {MeterName}", name);
                        return new NoOpMeter();
                    }
                }
                return meter;
            }
        }

        public Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
            where T : struct
        {
            if (!IsEnabled)
                return new NoOpCounter<T>();

            try
            {
                var meter = GetMeter(MeterName);
                var counter = meter.CreateCounter<T>(name, unit, description);
                _logger.LogDebug("Created counter: {Name} [{Unit}]", name, unit ?? "count");
                return counter;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create counter: {Name}", name);
                return new NoOpCounter<T>();
            }
        }

        public ObservableGauge<T> CreateGauge<T>(
            string name,
            Func<T> observeValue,
            string? unit = null,
            string? description = null) where T : struct
        {
            if (!IsEnabled)
                return new NoOpObservableGauge<T>();

            try
            {
                var meter = GetMeter(MeterName);
                var gauge = meter.CreateObservableGauge(name, observeValue, unit, description);
                _logger.LogDebug("Created gauge: {Name} [{Unit}]", name, unit ?? "value");
                return gauge;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create gauge: {Name}", name);
                return new NoOpObservableGauge<T>();
            }
        }

        public Histogram<T> CreateHistogram<T>(
            string name,
            string? unit = null,
            string? description = null,
            double[]? buckets = null) where T : struct
        {
            if (!IsEnabled)
                return new NoOpHistogram<T>();

            try
            {
                var meter = GetMeter(MeterName);
                var histogram = meter.CreateHistogram<T>(name, unit, description);

                _logger.LogDebug(
                    "Created histogram: {Name} [{Unit}] with {BucketCount} buckets",
                    name, unit ?? "value", buckets?.Length ?? 0);

                return histogram;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create histogram: {Name}", name);
                return new NoOpHistogram<T>();
            }
        }

        public UpDownCounter<T> CreateUpDownCounter<T>(
            string name,
            string? unit = null,
            string? description = null) where T : struct
        {
            if (!IsEnabled)
                return new NoOpUpDownCounter<T>();

            try
            {
                var meter = GetMeter(MeterName);
                var counter = meter.CreateUpDownCounter<T>(name, unit, description);
                _logger.LogDebug("Created UpDownCounter: {Name} [{Unit}]", name, unit ?? "value");
                return counter;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create UpDownCounter: {Name}", name);
                return new NoOpUpDownCounter<T>();
            }
        }

        public virtual void RecordEvent(string eventName, Dictionary<string, object?>? tags = null)
        {
            if (!IsEnabled)
                return;

            try
            {
                _logger.LogDebug("Recorded event: {EventName} with {TagCount} tags",
                    eventName, tags?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record event: {EventName}", eventName);
            }
        }

        protected virtual string GetInstrumentationVersion()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "1.0.0";
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_metersLock)
                    {
                        foreach (var meter in _meters.Values)
                        {
                            meter?.Dispose();
                        }
                        _meters.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }
}
```

#### Step 2: Create No-Op Implementations
**File**: src/Orchestra.Core/Services/Metrics/NoOpMetrics.cs
**Tool Calls**: 2

```csharp
using System.Diagnostics.Metrics;

namespace Orchestra.Core.Services.Metrics
{
    /// <summary>
    /// No-op реализации метрик для отключенного состояния
    /// </summary>
    internal sealed class NoOpMeter : Meter
    {
        public NoOpMeter() : base("NoOp") { }
    }

    internal sealed class NoOpCounter<T> : Counter<T> where T : struct
    {
        protected override void Publish() { }
        public override void Add(T delta) { }
        public override void Add(T delta, KeyValuePair<string, object?> tag) { }
        public override void Add(T delta, params KeyValuePair<string, object?>[] tags) { }
        public override void Add(T delta, in TagList tagList) { }
    }

    internal sealed class NoOpHistogram<T> : Histogram<T> where T : struct
    {
        protected override void Publish() { }
        public override void Record(T value) { }
        public override void Record(T value, KeyValuePair<string, object?> tag) { }
        public override void Record(T value, params KeyValuePair<string, object?>[] tags) { }
        public override void Record(T value, in TagList tagList) { }
    }

    internal sealed class NoOpObservableGauge<T> : ObservableGauge<T> where T : struct
    {
        protected override void Publish() { }
        protected override IEnumerable<Measurement<T>> Observe()
        {
            return Enumerable.Empty<Measurement<T>>();
        }
    }

    internal sealed class NoOpUpDownCounter<T> : UpDownCounter<T> where T : struct
    {
        protected override void Publish() { }
        public override void Add(T delta) { }
        public override void Add(T delta, KeyValuePair<string, object?> tag) { }
        public override void Add(T delta, params KeyValuePair<string, object?>[] tags) { }
        public override void Add(T delta, in TagList tagList) { }
    }
}
```

### 2.3C: Register in Dependency Injection (10 minutes)

#### Step 1: Create Service Collection Extension
**File**: src/Orchestra.Core/Extensions/MetricsServiceExtensions.cs
**Tool Calls**: 3

```csharp
using Microsoft.Extensions.DependencyInjection;
using Orchestra.Core.Services.Metrics;

namespace Orchestra.Core.Extensions
{
    /// <summary>
    /// Расширения для регистрации сервисов метрик
    /// </summary>
    public static class MetricsServiceExtensions
    {
        /// <summary>
        /// Добавить базовую инфраструктуру метрик
        /// </summary>
        public static IServiceCollection AddMetricsInfrastructure(this IServiceCollection services)
        {
            // Register base metrics provider as transient since specific implementations will be singleton
            services.AddTransient<IMetricsProvider, DefaultMetricsProvider>();

            // Register specific metrics services as singletons
            services.AddSingleton<IEscalationMetricsService, EscalationMetricsService>();

            return services;
        }
    }

    /// <summary>
    /// Default implementation for testing
    /// </summary>
    internal class DefaultMetricsProvider : MetricsProvider
    {
        public DefaultMetricsProvider(
            IMeterFactory meterFactory,
            IOptions<OpenTelemetryOptions> options,
            ILogger<DefaultMetricsProvider> logger)
            : base(meterFactory, options, logger, "Orchestra.Default")
        {
        }
    }
}
```

#### Step 2: Update Program.cs
**File**: src/Orchestra.API/Program.cs (modification)
**Tool Calls**: 1

```csharp
// Add after OpenTelemetry configuration
builder.Services.AddMetricsInfrastructure();
```

## Integration Points

### Dependency Injection Registration
- MetricsProvider registered as base class
- Specific implementations (EscalationMetricsService) as singletons
- IMeterFactory provided by OpenTelemetry

### Error Handling Strategy
- All metric operations wrapped in try-catch
- Failures logged but don't affect application flow
- No-op implementations returned on errors

### Thread Safety
- Lock used for meter dictionary access
- Meters cached after creation
- Thread-safe metric recording built into OpenTelemetry

## Validation Checklist

### Implementation
- [ ] IMetricsProvider interface compiles
- [ ] MetricsProvider base class compiles
- [ ] No-op implementations compile
- [ ] Extension methods work

### Registration
- [ ] Services registered in DI
- [ ] IMeterFactory injectable
- [ ] Options pattern working

### Error Handling
- [ ] Metric failures don't crash app
- [ ] Errors logged appropriately
- [ ] No-op fallbacks working

## Testing

### Unit Test Examples
```csharp
[Fact]
public void MetricsProvider_WhenDisabled_ReturnsNoOp()
{
    // Arrange
    var options = Options.Create(new OpenTelemetryOptions { MetricsEnabled = false });
    var provider = new TestMetricsProvider(_meterFactory, options, _logger);

    // Act
    var counter = provider.CreateCounter<long>("test_counter");

    // Assert
    counter.Should().BeOfType<NoOpCounter<long>>();
}

[Fact]
public void MetricsProvider_CreatesValidMeter()
{
    // Arrange
    var provider = new TestMetricsProvider(_meterFactory, _options, _logger);

    // Act
    var meter = provider.GetMeter("TestMeter");

    // Assert
    meter.Should().NotBeNull();
    meter.Name.Should().Be("TestMeter");
}

[Fact]
public void MetricsProvider_HandlesCreationErrors()
{
    // Arrange
    var mockFactory = new Mock<IMeterFactory>();
    mockFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>()))
        .Throws(new InvalidOperationException("Test error"));

    var provider = new TestMetricsProvider(mockFactory.Object, _options, _logger);

    // Act
    var meter = provider.GetMeter("FailingMeter");

    // Assert
    meter.Should().BeOfType<NoOpMeter>();
}
```

## Common Issues and Solutions

### Issue 1: Meter Not Recording
**Solution**: Verify meter name matches pattern in AddMeter() configuration

### Issue 2: Memory Leak with Meters
**Solution**: Ensure Dispose() is called, meters are cached properly

### Issue 3: Thread Contention
**Solution**: Use concurrent collections if high contention observed

## Next Steps

1. Proceed to Task 3: Implement EscalationMetricsService
2. Test MetricsProvider with simple metrics
3. Verify no-op behavior when disabled

## Success Criteria

- ✅ MetricsProvider base class functional
- ✅ No-op implementations prevent errors when disabled
- ✅ Thread-safe meter creation and caching
- ✅ Proper error handling and logging
- ✅ Dependency injection working correctly