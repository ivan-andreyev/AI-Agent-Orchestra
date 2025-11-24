using MediatR;
using Orchestra.Core.Options.Dtos;

namespace Orchestra.Core.Commands.Settings;

/// <summary>
/// Запрос для получения текущих настроек Telegram бота
/// </summary>
public record GetTelegramSettingsQuery : IRequest<TelegramSettingsDto>;
