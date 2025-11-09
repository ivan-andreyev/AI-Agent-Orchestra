using MediatR;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Services;

namespace Orchestra.Core.Commands.Permissions;

/// <summary>
/// Обработчик команды запроса одобрения от человека
/// </summary>
/// <remarks>
/// Обработчик:
/// 1. Логирует запрос одобрения
/// 2. Отправляет эскалацию через TelegramEscalationService
/// 3. Сохраняет запрос в базу данных (если нужно)
/// 4. Возвращает ApprovalId для отслеживания статуса
/// </remarks>
public class RequestHumanApprovalCommandHandler : IRequestHandler<RequestHumanApprovalCommand, RequestHumanApprovalResult>
{
    private readonly ILogger<RequestHumanApprovalCommandHandler> _logger;
    private readonly ITelegramEscalationService _telegramService;
    private readonly OrchestraDbContext _context;

    public RequestHumanApprovalCommandHandler(
        ILogger<RequestHumanApprovalCommandHandler> logger,
        ITelegramEscalationService telegramService,
        OrchestraDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
            // Формируем сообщение об эскалации
            var escalationMessage = BuildEscalationMessage(request);

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

                // Даже если Telegram не работает, создаём ApprovalId для отслеживания
            }

            // Создаём уникальный ID одобрения для отслеживания
            var approvalId = $"{request.AgentId}_{request.SessionId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

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
    private static string BuildEscalationMessage(RequestHumanApprovalCommand request)
    {
        var message = "Permission Denials Detected\n\n";
        message += $"Сессия: {request.SessionId}\n";
        message += $"Команда: {request.OriginalCommand}\n\n";

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
        message += $"/approve {request.SessionId}";

        return message;
    }
}
