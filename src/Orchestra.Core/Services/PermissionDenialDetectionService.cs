using Microsoft.Extensions.Logging;
using Orchestra.Core.Models;
using Orchestra.Core.Services.Connectors;
using System.Text.Json;

namespace Orchestra.Core.Services;

/// <summary>
/// Сервис для детектирования permission_denials в JSON ответах Claude Code
/// </summary>
/// <remarks>
/// Парсит JSON ответы из Claude Code и извлекает список permission_denials,
/// которые требуют одобрения от человека.
/// </remarks>
public interface IPermissionDenialDetectionService
{
    /// <summary>
    /// Парсит JSON строку и извлекает permission_denials если они есть
    /// </summary>
    /// <param name="jsonResponse">JSON ответ от Claude Code</param>
    /// <returns>ClaudeResponse с заполненным полем PermissionDenials или null</returns>
    ClaudeResponse? TryParseResponse(string jsonResponse);

    /// <summary>
    /// Проверяет содержит ли ответ permission_denials
    /// </summary>
    /// <param name="response">Распарсенный ответ</param>
    /// <returns>True если есть permission_denials</returns>
    bool HasPermissionDenials(ClaudeResponse? response);
}

/// <summary>
/// Реализация сервиса детектирования permission_denials
/// </summary>
public class PermissionDenialDetectionService : IPermissionDenialDetectionService
{
    private readonly ILogger<PermissionDenialDetectionService> _logger;

    public PermissionDenialDetectionService(ILogger<PermissionDenialDetectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Парсит JSON ответ и извлекает permission_denials
    /// </summary>
    public ClaudeResponse? TryParseResponse(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
        {
            return null;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            var response = JsonSerializer.Deserialize<ClaudeResponse>(jsonResponse, options);

            if (response != null && response.PermissionDenials != null && response.PermissionDenials.Count > 0)
            {
                _logger.LogWarning(
                    "Детектированы permission_denials в ответе. Количество: {Count}",
                    response.PermissionDenials.Count);

                foreach (var denial in response.PermissionDenials)
                {
                    _logger.LogWarning(
                        "  - Инструмент: {ToolName}, ID: {ToolUseId}",
                        denial.ToolName,
                        denial.ToolUseId ?? "unknown");
                }
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(
                ex,
                "Не удалось распарсить JSON ответ. Может быть это не JSON или некорректный формат.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при парсинге JSON ответа");
            return null;
        }
    }

    /// <summary>
    /// Проверяет содержит ли ответ permission_denials
    /// </summary>
    public bool HasPermissionDenials(ClaudeResponse? response)
    {
        return response != null
            && response.PermissionDenials != null
            && response.PermissionDenials.Count > 0;
    }
}
