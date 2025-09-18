using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Orchestra.Web.Services;

/// <summary>
/// Service for continuous performance monitoring with regression detection.
/// Implements baseline thresholds and automated monitoring as required by Phase 0.2.
/// </summary>
public class PerformanceMonitoringService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics = new();
    private readonly PerformanceThresholds _thresholds;

    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
    {
        _logger = logger;
        _thresholds = InitializeThresholds();
    }

    /// <summary>
    /// Performance thresholds based on Phase 0.1 baseline measurements
    /// </summary>
    private static PerformanceThresholds InitializeThresholds()
    {
        return new PerformanceThresholds
        {
            // API Performance Thresholds (200% of baseline for regression detection)
            ApiResponseTime = new ThresholdConfig
            {
                StateEndpoint = 165, // Current: 78.33 ms
                AgentsEndpoint = 130, // Current: 64.72 ms
                RepositoriesEndpoint = 165, // Current: 81.10 ms
                TaskQueueEndpoint = 215 // Current: 106.30 ms (needs fixing)
            },

            // Component Performance Targets (10% increase tolerance)
            ComponentPerformance = new ThresholdConfig
            {
                InitialPageLoad = 2000, // Target for Phase 1
                StatisticsCalculation = 100, // 221 agent aggregation
                ComponentRerender = 50, // Per component
                StateUpdatePropagation = 1000 // Plan requirement
            },

            // Memory Usage Thresholds (10% increase tolerance)
            MemoryUsage = new ThresholdConfig
            {
                BaselineIncrease = 10, // Percentage
                MaxMemoryGrowth = 50 // MB
            }
        };
    }

    /// <summary>
    /// Measure API response time and check against thresholds
    /// </summary>
    public async Task<PerformanceResult> MeasureApiResponseAsync(string endpoint, Func<Task> apiCall)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            await apiCall();
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        finally
        {
            stopwatch.Stop();
        }

        var result = new PerformanceResult
        {
            Endpoint = endpoint,
            Duration = stopwatch.ElapsedMilliseconds,
            Timestamp = DateTime.UtcNow,
            Success = exception == null,
            Exception = exception
        };

        // Check against thresholds
        var threshold = GetApiThreshold(endpoint);
        if (result.Duration > threshold)
        {
            _logger.LogWarning("API Performance Regression Detected: {Endpoint} took {Duration}ms (threshold: {Threshold}ms)",
                endpoint, result.Duration, threshold);
            result.IsRegression = true;
        }

        // Store metric for trend analysis
        _metrics.AddOrUpdate($"api_{endpoint}",
            new PerformanceMetric { LastValue = result.Duration, LastUpdated = DateTime.UtcNow, Threshold = threshold },
            (key, existing) =>
            {
                existing.LastValue = result.Duration;
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });

        return result;
    }

    /// <summary>
    /// Measure component render time and check against thresholds
    /// </summary>
    public PerformanceResult MeasureComponentRender(string componentName, Action renderAction)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            renderAction();
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        finally
        {
            stopwatch.Stop();
        }

        var result = new PerformanceResult
        {
            Endpoint = $"component_{componentName}",
            Duration = stopwatch.ElapsedMilliseconds,
            Timestamp = DateTime.UtcNow,
            Success = exception == null,
            Exception = exception
        };

        // Check against component thresholds
        var threshold = _thresholds.ComponentPerformance.ComponentRerender;
        if (result.Duration > threshold)
        {
            _logger.LogWarning("Component Performance Regression Detected: {Component} took {Duration}ms (threshold: {Threshold}ms)",
                componentName, result.Duration, threshold);
            result.IsRegression = true;
        }

        // Store metric for trend analysis
        _metrics.AddOrUpdate($"component_{componentName}",
            new PerformanceMetric { LastValue = result.Duration, LastUpdated = DateTime.UtcNow, Threshold = threshold },
            (key, existing) =>
            {
                existing.LastValue = result.Duration;
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });

        return result;
    }

    /// <summary>
    /// Measure statistics calculation performance (221 agent aggregation)
    /// </summary>
    public PerformanceResult MeasureStatisticsCalculation(Func<object> calculationFunc)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;
        object? result = null;

        try
        {
            result = calculationFunc();
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        finally
        {
            stopwatch.Stop();
        }

        var perfResult = new PerformanceResult
        {
            Endpoint = "statistics_calculation",
            Duration = stopwatch.ElapsedMilliseconds,
            Timestamp = DateTime.UtcNow,
            Success = exception == null,
            Exception = exception
        };

        // Check against statistics calculation threshold
        var threshold = _thresholds.ComponentPerformance.StatisticsCalculation;
        if (perfResult.Duration > threshold)
        {
            _logger.LogWarning("Statistics Calculation Performance Regression Detected: took {Duration}ms (threshold: {Threshold}ms)",
                perfResult.Duration, threshold);
            perfResult.IsRegression = true;
        }

        return perfResult;
    }

    /// <summary>
    /// Get current performance metrics summary
    /// </summary>
    public PerformanceMetricsSummary GetMetricsSummary()
    {
        var now = DateTime.UtcNow;
        var recentMetrics = _metrics.Where(m => now - m.Value.LastUpdated < TimeSpan.FromMinutes(5)).ToList();

        return new PerformanceMetricsSummary
        {
            TotalMetrics = _metrics.Count,
            RecentMetrics = recentMetrics.Count,
            RegressionsDetected = recentMetrics.Count(m => m.Value.LastValue > m.Value.Threshold),
            ApiMetrics = recentMetrics.Where(m => m.Key.StartsWith("api_")).Count(),
            ComponentMetrics = recentMetrics.Where(m => m.Key.StartsWith("component_")).Count(),
            LastUpdated = recentMetrics.Any() ? recentMetrics.Max(m => m.Value.LastUpdated) : null
        };
    }

    /// <summary>
    /// Get specific metric by name
    /// </summary>
    public PerformanceMetric? GetMetric(string metricName)
    {
        return _metrics.TryGetValue(metricName, out var metric) ? metric : null;
    }

    /// <summary>
    /// Get all metrics matching a pattern
    /// </summary>
    public Dictionary<string, PerformanceMetric> GetMetrics(string pattern)
    {
        return _metrics.Where(m => m.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                      .ToDictionary(m => m.Key, m => m.Value);
    }

    /// <summary>
    /// Clear old metrics (older than specified time)
    /// </summary>
    public void ClearOldMetrics(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var keysToRemove = _metrics.Where(m => m.Value.LastUpdated < cutoff).Select(m => m.Key).ToList();

        foreach (var key in keysToRemove)
        {
            _metrics.TryRemove(key, out _);
        }

        if (keysToRemove.Any())
        {
            _logger.LogInformation("Cleared {Count} old performance metrics", keysToRemove.Count);
        }
    }

    private double GetApiThreshold(string endpoint)
    {
        return endpoint.ToLowerInvariant() switch
        {
            "state" => _thresholds.ApiResponseTime.StateEndpoint,
            "agents" => _thresholds.ApiResponseTime.AgentsEndpoint,
            "repositories" => _thresholds.ApiResponseTime.RepositoriesEndpoint,
            "tasks/queue" => _thresholds.ApiResponseTime.TaskQueueEndpoint,
            _ => 200 // Default threshold
        };
    }
}

/// <summary>
/// Performance measurement result
/// </summary>
public class PerformanceResult
{
    public string Endpoint { get; set; } = string.Empty;
    public long Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public bool IsRegression { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Individual performance metric with threshold
/// </summary>
public class PerformanceMetric
{
    public long LastValue { get; set; }
    public DateTime LastUpdated { get; set; }
    public double Threshold { get; set; }
}

/// <summary>
/// Performance metrics summary
/// </summary>
public class PerformanceMetricsSummary
{
    public int TotalMetrics { get; set; }
    public int RecentMetrics { get; set; }
    public int RegressionsDetected { get; set; }
    public int ApiMetrics { get; set; }
    public int ComponentMetrics { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Performance thresholds configuration based on Phase 0.1 baseline
/// </summary>
public class PerformanceThresholds
{
    public ThresholdConfig ApiResponseTime { get; set; } = new();
    public ThresholdConfig ComponentPerformance { get; set; } = new();
    public ThresholdConfig MemoryUsage { get; set; } = new();
}

/// <summary>
/// Threshold configuration values
/// </summary>
public class ThresholdConfig
{
    // API thresholds (milliseconds)
    public double StateEndpoint { get; set; }
    public double AgentsEndpoint { get; set; }
    public double RepositoriesEndpoint { get; set; }
    public double TaskQueueEndpoint { get; set; }

    // Component thresholds (milliseconds)
    public double InitialPageLoad { get; set; }
    public double StatisticsCalculation { get; set; }
    public double ComponentRerender { get; set; }
    public double StateUpdatePropagation { get; set; }

    // Memory thresholds
    public double BaselineIncrease { get; set; }
    public double MaxMemoryGrowth { get; set; }
}