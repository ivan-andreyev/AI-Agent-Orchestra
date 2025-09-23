using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Orchestra.Web.Services;

/// <summary>
/// Centralized logging service for web components with structured logging and debugging capabilities
/// </summary>
public class LoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs component lifecycle events with context
    /// </summary>
    public void LogComponentLifecycle(string componentName, string lifecycleEvent, object? context = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var contextJson = context != null ? JsonSerializer.Serialize(context) : null;
            _logger.LogInformation("Component lifecycle: {LifecycleEvent}. Context: {Context}",
                lifecycleEvent, contextJson ?? "None");
        }
    }

    /// <summary>
    /// Logs SignalR connection events with detailed information
    /// </summary>
    public void LogSignalREvent(string hubName, string eventType, string? connectionId = null, object? data = null)
    {
        using (_logger.BeginScope("SignalR Hub: {HubName}", hubName))
        {
            var dataJson = data != null ? JsonSerializer.Serialize(data) : null;
            _logger.LogInformation("SignalR event: {EventType}. ConnectionId: {ConnectionId}. Data: {Data}",
                eventType, connectionId ?? "Unknown", dataJson ?? "None");
        }
    }

    /// <summary>
    /// Logs state changes in components with before/after comparison
    /// </summary>
    public void LogStateChange(string componentName, string stateName, object? previousValue, object? newValue)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var previousJson = previousValue != null ? JsonSerializer.Serialize(previousValue) : null;
            var newJson = newValue != null ? JsonSerializer.Serialize(newValue) : null;

            _logger.LogDebug("State change: {StateName}. Previous: {PreviousValue}. New: {NewValue}",
                stateName, previousJson ?? "null", newJson ?? "null");
        }
    }

    /// <summary>
    /// Logs performance metrics for component operations
    /// </summary>
    public void LogPerformanceMetric(string componentName, string operationName, long durationMs, object? metadata = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;

            var logLevel = durationMs switch
            {
                > 5000 => LogLevel.Warning,
                > 2000 => LogLevel.Information,
                _ => LogLevel.Debug
            };

            _logger.Log(logLevel, "Performance: {OperationName} took {DurationMs}ms. Metadata: {Metadata}",
                operationName, durationMs, metadataJson ?? "None");
        }
    }

    /// <summary>
    /// Logs user interaction events for debugging UI behavior
    /// </summary>
    public void LogUserInteraction(string componentName, string interactionType, object? interactionData = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var dataJson = interactionData != null ? JsonSerializer.Serialize(interactionData) : null;
            _logger.LogDebug("User interaction: {InteractionType}. Data: {Data}",
                interactionType, dataJson ?? "None");
        }
    }

    /// <summary>
    /// Logs error details with full context for debugging
    /// </summary>
    public void LogError(string componentName, string operationName, Exception exception, object? context = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var contextJson = context != null ? JsonSerializer.Serialize(context) : null;
            _logger.LogError(exception, "Error in operation: {OperationName}. Context: {Context}",
                operationName, contextJson ?? "None");
        }
    }

    /// <summary>
    /// Logs API call results for debugging backend communication
    /// </summary>
    public void LogApiCall(string componentName, string apiEndpoint, string method, int statusCode, long durationMs, object? requestData = null, object? responseData = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var requestJson = requestData != null ? JsonSerializer.Serialize(requestData) : null;
            var responseJson = responseData != null ? JsonSerializer.Serialize(responseData) : null;

            var logLevel = statusCode >= 400 ? LogLevel.Warning : LogLevel.Debug;

            _logger.Log(logLevel, "API call: {Method} {ApiEndpoint} returned {StatusCode} in {DurationMs}ms. Request: {RequestData}. Response: {ResponseData}",
                method, apiEndpoint, statusCode, durationMs, requestJson ?? "None", responseJson ?? "None");
        }
    }

    /// <summary>
    /// Logs data refresh operations with detailed timing
    /// </summary>
    public void LogDataRefresh(string componentName, string dataType, bool success, long durationMs, int? itemCount = null, string? errorMessage = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            if (success)
            {
                _logger.LogInformation("Data refresh: {DataType} completed successfully in {DurationMs}ms. Items: {ItemCount}",
                    dataType, durationMs, itemCount ?? 0);
            }
            else
            {
                _logger.LogWarning("Data refresh: {DataType} failed after {DurationMs}ms. Error: {ErrorMessage}",
                    dataType, durationMs, errorMessage ?? "Unknown error");
            }
        }
    }

    /// <summary>
    /// Logs navigation and routing events
    /// </summary>
    public void LogNavigation(string componentName, string fromLocation, string toLocation, object? navigationData = null)
    {
        using (_logger.BeginScope("Component: {ComponentName}", componentName))
        {
            var dataJson = navigationData != null ? JsonSerializer.Serialize(navigationData) : null;
            _logger.LogDebug("Navigation: From {FromLocation} to {ToLocation}. Data: {NavigationData}",
                fromLocation, toLocation, dataJson ?? "None");
        }
    }

    /// <summary>
    /// Creates a scope for measuring operation duration
    /// </summary>
    public IDisposable MeasureOperation(string componentName, string operationName, object? context = null)
    {
        return new OperationScope(_logger, componentName, operationName, context);
    }

    /// <summary>
    /// Helper class for measuring operation duration with automatic logging
    /// </summary>
    private class OperationScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _componentName;
        private readonly string _operationName;
        private readonly object? _context;
        private readonly DateTime _startTime;
        private bool _disposed = false;

        public OperationScope(ILogger logger, string componentName, string operationName, object? context)
        {
            _logger = logger;
            _componentName = componentName;
            _operationName = operationName;
            _context = context;
            _startTime = DateTime.UtcNow;

            var contextJson = context != null ? JsonSerializer.Serialize(context) : null;
            using (_logger.BeginScope("Component: {ComponentName}", componentName))
            {
                _logger.LogDebug("Operation started: {OperationName}. Context: {Context}",
                    operationName, contextJson ?? "None");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                var duration = DateTime.UtcNow - _startTime;
                var durationMs = (long)duration.TotalMilliseconds;

                using (_logger.BeginScope("Component: {ComponentName}", _componentName))
                {
                    var logLevel = durationMs switch
                    {
                        > 5000 => LogLevel.Warning,
                        > 2000 => LogLevel.Information,
                        _ => LogLevel.Debug
                    };

                    _logger.Log(logLevel, "Operation completed: {OperationName} in {DurationMs}ms",
                        _operationName, durationMs);
                }

                _disposed = true;
            }
        }
    }
}