using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Commands.Permissions;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Options;
using Orchestra.Core.Services.Metrics;

namespace Orchestra.Core.Services;

/// <summary>
/// Background service для автоматической отмены approval requests при превышении timeout
/// </summary>
/// <remarks>
/// Сервис:
/// 1. Запускается автоматически с приложением как IHostedService
/// 2. Проверяет БД на наличие expired approval requests каждые N секунд (CheckIntervalSeconds)
/// 3. Вызывает CancelApprovalCommand для каждого expired approval
/// 4. Graceful shutdown на StopAsync
/// 5. Ограничивает количество одновременно обрабатываемых timeouts (MaxConcurrentTimeouts)
/// </remarks>
public class ApprovalTimeoutService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalTimeoutService> _logger;
    private readonly IOptions<ApprovalTimeoutOptions> _options;
    private readonly EscalationMetricsService _metricsService;
    private readonly TimeSpan _checkInterval;

    public ApprovalTimeoutService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalTimeoutService> logger,
        IOptions<ApprovalTimeoutOptions> options,
        EscalationMetricsService metricsService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _checkInterval = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
    }

    /// <summary>
    /// Основной метод background service для проверки expired approvals
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ApprovalTimeoutService запущен. CheckInterval: {CheckInterval}s, DefaultTimeout: {DefaultTimeout}m",
            _options.Value.CheckIntervalSeconds,
            _options.Value.DefaultTimeoutMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForExpiredApprovalsAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("ApprovalTimeoutService получил сигнал остановки");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка при проверке expired approvals. Повтор через {RetryDelay}s",
                    _checkInterval.TotalSeconds);

                // Continue running even if there's an error
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        _logger.LogInformation("ApprovalTimeoutService остановлен");
    }

    /// <summary>
    /// Проверяет БД на наличие expired approval requests и отменяет их
    /// </summary>
    private async Task CheckForExpiredApprovalsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var gracePeriod = TimeSpan.FromSeconds(_options.Value.GracePeriodSeconds);
            var expirationCutoff = DateTime.UtcNow - gracePeriod;

            // Находим все pending approvals, которые истекли
            var expiredApprovals = await context.ApprovalRequests
                .Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt <= expirationCutoff)
                .OrderBy(a => a.ExpiresAt)
                .Take(_options.Value.MaxConcurrentTimeouts)
                .ToListAsync(cancellationToken);

            if (expiredApprovals.Count == 0)
            {
                return;
            }

            _logger.LogWarning(
                "Найдено {ExpiredCount} expired approval requests. Начинаем отмену...",
                expiredApprovals.Count);

            // Отменяем каждый expired approval
            foreach (var approval in expiredApprovals)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var timeoutDuration = DateTime.UtcNow - approval.CreatedAt;

                    _logger.LogWarning(
                        "Approval request истёк по таймауту. ApprovalId: {ApprovalId}, " +
                        "SessionId: {SessionId}, AgentId: {AgentId}, Duration: {Duration:mm\\:ss}",
                        approval.ApprovalId,
                        approval.SessionId,
                        approval.AgentId,
                        timeoutDuration);

                    // Вызываем команду отмены через MediatR
                    await mediator.Send(
                        new CancelApprovalCommand(approval.ApprovalId, "Timeout"),
                        cancellationToken);

                    // Record metrics: Approval timeout
                    try
                    {
                        _metricsService.RecordApprovalTimeout(approval.ApprovalId);
                        _metricsService.RecordEscalationDequeued(approval.ApprovalId);

                        _logger.LogDebug(
                            "Метрики таймаута approval записаны. ApprovalId: {ApprovalId}",
                            approval.ApprovalId);
                    }
                    catch (Exception metricsEx)
                    {
                        _logger.LogWarning(
                            metricsEx,
                            "Не удалось записать метрики таймаута approval. ApprovalId: {ApprovalId}",
                            approval.ApprovalId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Ошибка при отмене approval request {ApprovalId}",
                        approval.ApprovalId);
                    // Продолжаем обработку остальных expired approvals
                }
            }

            _logger.LogInformation(
                "Обработка expired approvals завершена. Отменено: {CancelledCount}",
                expiredApprovals.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке expired approvals в БД");
            throw;
        }
    }

    /// <summary>
    /// Graceful shutdown при остановке приложения
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ApprovalTimeoutService останавливается...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("ApprovalTimeoutService успешно остановлен");
    }
}
