using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Core.Commands.Settings;
using Orchestra.Core.Options.Dtos;

namespace Orchestra.API.Controllers;

/// <summary>
/// API контроллер для управления настройками приложения
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SettingsController> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр SettingsController
    /// </summary>
    /// <param name="mediator">Медиатор для выполнения команд и запросов</param>
    /// <param name="logger">Логгер</param>
    public SettingsController(IMediator mediator, ILogger<SettingsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Получить текущие настройки Telegram бота
    /// </summary>
    /// <returns>Настройки Telegram с маскированным токеном</returns>
    [HttpGet("telegram")]
    [ProducesResponseType(typeof(TelegramSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TelegramSettingsDto>> GetTelegramSettings()
    {
        try
        {
            _logger.LogDebug("Запрос настроек Telegram");

            var query = new GetTelegramSettingsQuery();
            var result = await _mediator.Send(query);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении настроек Telegram");
            return StatusCode(500, new { Message = "Не удалось получить настройки Telegram", Error = ex.Message });
        }
    }

    /// <summary>
    /// Обновить настройки Telegram бота
    /// </summary>
    /// <param name="request">Новые настройки</param>
    /// <returns>Результат обновления</returns>
    [HttpPut("telegram")]
    [ProducesResponseType(typeof(UpdateTelegramSettingsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UpdateTelegramSettingsResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpdateTelegramSettingsResult>> UpdateTelegramSettings(
        [FromBody] UpdateTelegramSettingsRequest request)
    {
        try
        {
            _logger.LogInformation("Обновление настроек Telegram. Enabled: {Enabled}", request.Enabled);

            var command = new UpdateTelegramSettingsCommand(
                request.BotToken,
                request.ChatId,
                request.Enabled);

            var result = await _mediator.Send(command);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении настроек Telegram");
            return StatusCode(500, new { Message = "Не удалось обновить настройки Telegram", Error = ex.Message });
        }
    }

    /// <summary>
    /// Протестировать подключение к Telegram боту
    /// </summary>
    /// <param name="request">Параметры для тестирования</param>
    /// <returns>Результат тестирования</returns>
    [HttpPost("telegram/test")]
    [ProducesResponseType(typeof(TestTelegramConnectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TestTelegramConnectionResult>> TestTelegramConnection(
        [FromBody] TestTelegramConnectionRequest request)
    {
        try
        {
            _logger.LogInformation("Тестирование подключения к Telegram");

            var command = new TestTelegramConnectionCommand(
                request.BotToken,
                request.ChatId);

            var result = await _mediator.Send(command);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при тестировании подключения к Telegram");
            return StatusCode(500, new TestTelegramConnectionResult(
                Success: false,
                Message: $"Внутренняя ошибка сервера: {ex.Message}",
                ErrorCode: "INTERNAL_ERROR"));
        }
    }
}

#region Request Models

/// <summary>
/// Запрос на обновление настроек Telegram
/// </summary>
/// <param name="BotToken">Токен бота</param>
/// <param name="ChatId">ID чата</param>
/// <param name="Enabled">Включить интеграцию</param>
public record UpdateTelegramSettingsRequest(
    string BotToken,
    string ChatId,
    bool Enabled);

/// <summary>
/// Запрос на тестирование подключения к Telegram
/// </summary>
/// <param name="BotToken">Токен бота для тестирования</param>
/// <param name="ChatId">ID чата для отправки тестового сообщения</param>
public record TestTelegramConnectionRequest(
    string BotToken,
    string ChatId);

#endregion
