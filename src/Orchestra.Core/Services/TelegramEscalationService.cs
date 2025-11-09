using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestra.Core.Options;
using Orchestra.Core.Services.Metrics;
using Orchestra.Core.Services.Resilience;
using Orchestra.Core.Utilities;
using Polly;
using System.Diagnostics;
using System.Text.Json;

namespace Orchestra.Core.Services;

/// <summary>
/// Реализация сервиса эскалации через Telegram
/// </summary>
/// <remarks>
/// Сервис отправляет запросы на одобрение через Telegram бота.
/// Требует конфигурацию BotToken и ChatId в appsettings.json.
/// Использует Polly retry policy для resilient вызовов API.
/// </remarks>
public class TelegramEscalationService : ITelegramEscalationService
{
    private readonly ILogger<TelegramEscalationService> _logger;
    private readonly TelegramEscalationOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPolicy;
    private readonly EscalationMetricsService? _metricsService;

    /// <summary>
    /// Базовый URL Telegram Bot API
    /// </summary>
    private const string TelegramApiUrl = "https://api.telegram.org/bot";

    public TelegramEscalationService(
        ILogger<TelegramEscalationService> logger,
        IOptions<TelegramEscalationOptions> options,
        HttpClient httpClient,
        IPolicyRegistry policyRegistry,
        EscalationMetricsService? metricsService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _metricsService = metricsService;

        if (policyRegistry == null)
        {
            throw new ArgumentNullException(nameof(policyRegistry));
        }

        _retryPolicy = policyRegistry.GetTelegramRetryPolicy();

        _logger.LogInformation(
            "TelegramEscalationService initialized. Enabled: {Enabled}, Configured: {Configured}, RetryPolicy: Configured, Metrics: {MetricsEnabled}",
            _options.Enabled,
            !string.IsNullOrEmpty(_options.BotToken),
            metricsService != null);
    }

    /// <summary>
    /// Отправляет эскалационное сообщение в Telegram
    /// </summary>
    public async Task<bool> SendEscalationAsync(
        string agentId,
        string message,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Проверяем конфигурацию
        if (!_options.Enabled)
        {
            _logger.LogWarning("TelegramEscalationService disabled in configuration");
            return false;
        }

        if (string.IsNullOrEmpty(_options.BotToken) || string.IsNullOrEmpty(_options.ChatId))
        {
            _logger.LogError(
                "TelegramEscalationService not configured. BotToken: {BotTokenExists}, ChatId: {ChatIdExists}",
                !string.IsNullOrEmpty(_options.BotToken),
                !string.IsNullOrEmpty(_options.ChatId));
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        var retryCount = 0;

        try
        {
            var escapedMessage = TelegramMarkdownHelper.EscapeMarkdownV2(message);
            var telegramMessage = $"*⚠️ Operator Approval Required*\n\n" +
                                 $"Agent: `{TelegramMarkdownHelper.EscapeMarkdownV2(agentId)}`\n\n" +
                                 $"{escapedMessage}";

            // Подготавливаем payload для Telegram API
            var requestPayload = new
            {
                chat_id = _options.ChatId,
                text = telegramMessage,
                parse_mode = "MarkdownV2",
                disable_notification = false
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var url = $"{TelegramApiUrl}{_options.BotToken}/sendMessage";

            // Используем retry policy для resilient вызова
            var response = await _retryPolicy.ExecuteAsync(
                async ct =>
                {
                    try
                    {
                        return await _httpClient.PostAsync(url, jsonContent, ct);
                    }
                    catch (Exception)
                    {
                        // Record retry attempt on exception
                        retryCount++;
                        _metricsService?.RecordTelegramRetryAttempt(agentId, retryCount);
                        throw;
                    }
                },
                cancellationToken);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                // Record metrics: Successful Telegram call
                _metricsService?.RecordTelegramMessageSent(agentId, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogInformation(
                    "Escalation message successfully sent to Telegram for agent {AgentId} in {Duration:F2}ms",
                    agentId,
                    stopwatch.Elapsed.TotalMilliseconds);
                return true;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Record metrics: Failed Telegram call
            var errorCode = (int)response.StatusCode;
            _metricsService?.RecordTelegramMessageFailed(agentId, errorCode);

            _logger.LogError(
                "Error sending message to Telegram. StatusCode: {StatusCode}, Response: {Response}",
                response.StatusCode,
                responseContent);
            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record metrics: Failed Telegram call (exception)
            _metricsService?.RecordTelegramMessageFailed(agentId, null);

            _logger.LogError(
                ex,
                "Exception sending escalation message to Telegram for agent {AgentId}",
                agentId);
            return false;
        }
    }

    /// <summary>
    /// Проверяет доступность Telegram API
    /// </summary>
    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        if (string.IsNullOrEmpty(_options.BotToken))
        {
            return false;
        }

        try
        {
            var url = $"{TelegramApiUrl}{_options.BotToken}/getMe";

            // Используем retry policy для проверки подключения
            var response = await _retryPolicy.ExecuteAsync(
                async ct => await _httpClient.GetAsync(url, ct),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking connection to Telegram API");
            return false;
        }
    }

    /// <summary>
    /// Получает статус конфигурации сервиса
    /// </summary>
    public string GetConfigurationStatus()
    {
        if (!_options.Enabled)
        {
            return "disabled";
        }

        if (string.IsNullOrEmpty(_options.BotToken) || string.IsNullOrEmpty(_options.ChatId))
        {
            return "not_configured";
        }

        return "configured";
    }
}
