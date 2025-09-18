using Microsoft.AspNetCore.Components;

namespace Orchestra.Web.Components.Base;

/// <summary>
/// Base component that provides auto-refresh functionality with configurable intervals.
/// Eliminates code duplication across components that need periodic data refreshes.
/// </summary>
public abstract class AutoRefreshComponent : ComponentBase, IDisposable
{
    private Timer? _refreshTimer;
    private bool _autoRefresh = false;
    private TimeSpan _refreshInterval = TimeSpan.FromSeconds(5); // Default 5 seconds

    /// <summary>
    /// Gets or sets whether auto-refresh is currently enabled.
    /// </summary>
    protected bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            if (_autoRefresh != value)
            {
                _autoRefresh = value;
                ToggleAutoRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the refresh interval. Default is 5 seconds.
    /// Changes take effect on the next auto-refresh toggle.
    /// </summary>
    protected TimeSpan RefreshInterval
    {
        get => _refreshInterval;
        set => _refreshInterval = value;
    }

    /// <summary>
    /// Abstract method that derived components must implement to define their refresh logic.
    /// This method will be called periodically when auto-refresh is enabled.
    /// </summary>
    protected abstract Task RefreshDataAsync();

    /// <summary>
    /// Toggles the auto-refresh functionality on or off.
    /// When enabled, starts a timer that calls RefreshDataAsync at the configured interval.
    /// When disabled, stops and disposes the timer.
    /// </summary>
    protected void ToggleAutoRefresh()
    {
        if (_autoRefresh)
        {
            // Start auto-refresh timer
            _refreshTimer = new Timer(async _ =>
            {
                await InvokeAsync(async () => await RefreshDataAsync());
            }, null, _refreshInterval, _refreshInterval);
        }
        else
        {
            // Stop auto-refresh
            StopAutoRefresh();
        }
    }

    /// <summary>
    /// Stops the auto-refresh timer without changing the AutoRefresh property.
    /// Useful for temporarily stopping refresh during component lifecycle events.
    /// </summary>
    protected void StopAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    /// <summary>
    /// Manually triggers a refresh by calling RefreshDataAsync.
    /// Can be called regardless of auto-refresh state.
    /// </summary>
    protected async Task ManualRefreshAsync()
    {
        await RefreshDataAsync();
    }

    /// <summary>
    /// Sets a custom refresh interval and restarts auto-refresh if currently enabled.
    /// </summary>
    /// <param name="interval">The new refresh interval</param>
    protected void SetRefreshInterval(TimeSpan interval)
    {
        _refreshInterval = interval;

        // If auto-refresh is currently enabled, restart with new interval
        if (_autoRefresh)
        {
            StopAutoRefresh();
            ToggleAutoRefresh();
        }
    }

    /// <summary>
    /// Disposes the refresh timer to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose pattern implementation.
    /// </summary>
    /// <param name="disposing">True if disposing from Dispose method, false if from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAutoRefresh();
        }
    }
}