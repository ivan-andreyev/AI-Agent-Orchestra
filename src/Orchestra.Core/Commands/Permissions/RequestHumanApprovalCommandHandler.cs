using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Data;
using Orchestra.Core.Data.Entities;
using Orchestra.Core.Options;
using Orchestra.Core.Services;
using System.Text.Json;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Обработчик команды запроса одобрения от человека
/// </summary>
/// <remarks>
/// Обработчик:
/// 1. Логирует запрос одобрения
/// 2. Создаёт ApprovalRequest в БД с timeout
/// 3. Отправляет эскалацию через TelegramEscalationService
/// 4. Возвращает ApprovalId для отслеживания статуса
/// </remarks>
public class RequestHumanApprovalCommandHandler : IRequestHandler<RequestHumanApprovalCommand, RequestHumanApprovalResult>
{
    private readonly ILogger<RequestHumanApprovalCommandHandler> _logger;
    private readonly ITelegramEscalationService _telegramService;
    private readonly OrchestraDbContext _context;
    private readonly IOptions<ApprovalTimeoutOptions> _timeoutOptions;

    public RequestHumanApprovalCommandHandler(
        ILogger<RequestHumanApprovalCommandHandler> logger,
        ITelegramEscalationService telegramService,
        OrchestraDbContext context,
        IOptions<ApprovalTimeoutOptions> timeoutOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeoutOptions = timeoutOptions ?? throw new ArgumentNullException(nameof(timeoutOptions));
    }

    /// <summary>
    /// Обрабатывает запрос одобрения от человека
    /// </summary>
    public async Task<RequestHumanApprovalResult> Handle(
        RequestHumanApprovalCommand request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _logger.LogWarning(
            "Требуется одобрение от человека для агента {AgentId}, SessionId: {SessionId}. " +
            "Количество запросов разрешений: {DenialCount}",
            request.AgentId,
            request.SessionId,
            request.PermissionDenials?.Count ?? 0);

        try
        {
            // Создаём уникальный ID одобрения для отслеживания
            var approvalId = $"{request.AgentId}_{request.SessionId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Вычисляем время истечения на основе конфигурации
            var timeoutMinutes = _timeoutOptions.Value.DefaultTimeoutMinutes;
            var expiresAt = DateTime.UtcNow.AddMinutes(timeoutMinutes);

            // Сериализуем детали запроса для хранения в БД
            string? requestDetails = null;
            if (request.PermissionDenials != null && request.PermissionDenials.Count > 0)
            {
                try
                {
                    requestDetails = JsonSerializer.Serialize(request.PermissionDenials);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Не удалось сериализовать PermissionDenials для ApprovalId: {ApprovalId}",
                        approvalId);
                }
            }

            // Создаём approval request в БД
            var approvalRequest = new ApprovalRequest
            {
                ApprovalId = approvalId,
                SessionId = request.SessionId,
                AgentId = request.AgentId,
                Status = ApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                RequestDetails = requestDetails
            };

            _context.ApprovalRequests.Add(approvalRequest);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Approval request сохранён в БД. ApprovalId: {ApprovalId}, AgentId: {AgentId}, " +
                "ExpiresAt: {ExpiresAt}, Timeout: {TimeoutMinutes}m",
                approvalId,
                request.AgentId,
                expiresAt,
                timeoutMinutes);

            // Формируем сообщение об эскалации
            var escalationMessage = BuildEscalationMessage(request, approvalId, timeoutMinutes);

            // Отправляем эскалацию через Telegram
            var telegramSuccess = await _telegramService.SendEscalationAsync(
                request.AgentId,
                escalationMessage,
                null,
                cancellationToken);

            if (!telegramSuccess)
            {
                _logger.LogWarning(
                    "Не удалось отправить эскалацию через Telegram для агента {AgentId}. " +
                    "Telegram может быть отключен или не сконфигурирован.",
                    request.AgentId);

                // Даже если Telegram не работает, ApprovalId уже создан в БД
            }

            _logger.LogInformation(
                "Запрос одобрения создан. ApprovalId: {ApprovalId}, AgentId: {AgentId}",
                approvalId,
                request.AgentId);

            return new RequestHumanApprovalResult(
                Success: true,
                Message: "Запрос одобрения отправлен оператору",
                ApprovalId: approvalId,
                ApprovedAt: null,
                ApprovedBy: null,
                IsApproved: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при обработке запроса одобрения для агента {AgentId}",
                request.AgentId);

            return new RequestHumanApprovalResult(
                Success: false,
                Message: $"Ошибка при отправке запроса: {ex.Message}");
        }
    }

    /// <summary>
    /// Формирует сообщение об эскалации для оператора
    /// </summary>
    private static string BuildEscalationMessage(RequestHumanApprovalCommand request, string approvalId, int timeoutMinutes)
    {
        var message = "⚠️ Permission Denials Detected\n\n";
        message += $"Approval ID: {approvalId}\n";
        message += $"Сессия: {request.SessionId}\n";
        message += $"Команда: {request.OriginalCommand}\n";
        message += $"Timeout: {timeoutMinutes} минут\n\n";

        if (request.PermissionDenials != null && request.PermissionDenials.Count > 0)
        {
            message += "Требуемые разрешения:\n";
            foreach (var denial in request.PermissionDenials)
            {
                message += $"- Инструмент: {denial.ToolName}\n";
                if (!string.IsNullOrEmpty(denial.ToolUseId))
                {
                    message += $"  ID: {denial.ToolUseId}\n";
                }
                if (denial.ToolInput != null)
                {
                    message += $"  Параметры: {string.Join(", ", denial.ToolInput.Keys)}\n";
                }
            }
        }

        message += "\nОтправьте одобрение через команду:\n";
        message += $"/approve {approvalId}";

        return message;
    }
}
