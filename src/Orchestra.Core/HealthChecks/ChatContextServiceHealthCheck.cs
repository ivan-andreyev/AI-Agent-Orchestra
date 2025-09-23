using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orchestra.Core.Services;

namespace Orchestra.Core.HealthChecks;

/// <summary>
/// Health check for ChatContextService availability and responsiveness
/// </summary>
public class ChatContextServiceHealthCheck : IHealthCheck
{
    private readonly IChatContextService _chatContextService;

    public ChatContextServiceHealthCheck(IChatContextService chatContextService)
    {
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Простая проверка доступности сервиса
            // Используем временный уникальный ID для health check без создания мусорных данных
            var healthCheckUserId = $"health-check-{Guid.NewGuid():N}";

            await _chatContextService.GetUserSessionsAsync(healthCheckUserId, cancellationToken);

            return HealthCheckResult.Healthy("Chat context service is responsive and database connection is working");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Chat context service health check was cancelled");
        }
        catch (TimeoutException ex)
        {
            return HealthCheckResult.Degraded("Chat context service is slow to respond", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Chat context service failed health check", ex);
        }
    }
}