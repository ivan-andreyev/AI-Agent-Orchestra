using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Data;
using Orchestra.Core.Models.Chat;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Реализация сервиса управления контекстом чата с использованием Entity Framework и кеширования
    /// </summary>
    public class ChatContextService : IChatContextService
    {
        private readonly OrchestraDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ChatContextService> _logger;

        /// <summary>
        /// Время жизни объектов в кеше
        /// </summary>
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Максимальное количество сообщений для загрузки по умолчанию
        /// </summary>
        private const int DefaultMessageLimit = 1000;

        /// <summary>
        /// Инициализирует новый экземпляр ChatContextService
        /// </summary>
        /// <param name="context">Контекст базы данных</param>
        /// <param name="cache">Сервис кеширования в памяти</param>
        /// <param name="logger">Логгер для записи событий</param>
        public ChatContextService(
            OrchestraDbContext context,
            IMemoryCache cache,
            ILogger<ChatContextService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ChatSession> GetOrCreateSessionAsync(
            string? userId,
            string instanceId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("Instance ID cannot be null or empty", nameof(instanceId));
            }

            _logger.LogDebug("Getting or creating session for userId: {UserId}, instanceId: {InstanceId}",
                userId ?? "[anonymous]", instanceId);

            try
            {
                // Проверяем кеш для существующей сессии
                var cacheKey = GetUserSessionsCacheKey(userId ?? "anonymous");
                if (_cache.TryGetValue(cacheKey, out List<ChatSession>? cachedSessions) && cachedSessions != null)
                {
                    var cachedSession = cachedSessions.FirstOrDefault(s => s.InstanceId == instanceId);
                    if (cachedSession != null)
                    {
                        _logger.LogDebug("Found cached session {SessionId} for user {UserId}",
                            cachedSession.Id, userId ?? "[anonymous]");
                        return cachedSession;
                    }
                }

                // Ищем существующую сессию в базе данных
                var existingSession = await _context.ChatSessions
                    .Where(s => s.UserId == userId && s.InstanceId == instanceId)
                    .OrderByDescending(s => s.LastMessageAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingSession != null)
                {
                    _logger.LogDebug("Found existing session {SessionId} in database", existingSession.Id);

                    // Обновляем кеш
                    await UpdateSessionCacheAsync(existingSession, cancellationToken);
                    return existingSession;
                }

                // Создаем новую сессию
                var newSession = new ChatSession
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    InstanceId = instanceId,
                    Title = $"Chat Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow,
                    Messages = new List<ChatMessage>()
                };

                _context.ChatSessions.Add(newSession);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Created new chat session {SessionId} for user {UserId} and instance {InstanceId}",
                    newSession.Id, userId ?? "[anonymous]", instanceId);

                // Добавляем в кеш
                await UpdateSessionCacheAsync(newSession, cancellationToken);

                return newSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating session for userId: {UserId}, instanceId: {InstanceId}",
                    userId ?? "[anonymous]", instanceId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ChatMessage> SaveMessageAsync(
            Guid sessionId,
            string author,
            string content,
            MessageType messageType,
            string? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
            }

            if (string.IsNullOrWhiteSpace(author))
            {
                throw new ArgumentException("Author cannot be null or empty", nameof(author));
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            }

            _logger.LogDebug("Saving message from {Author} to session {SessionId}", author, sessionId);

            try
            {
                // Проверяем существование сессии
                var session = await _context.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

                if (session == null)
                {
                    throw new InvalidOperationException($"Chat session with ID {sessionId} not found");
                }

                // Создаем новое сообщение
                var message = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Author = author,
                    Content = content,
                    MessageType = messageType,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = metadata
                };

                // Обновляем время последнего сообщения в сессии
                session.LastMessageAt = DateTime.UtcNow;

                _context.ChatMessages.Add(message);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Saved message {MessageId} from {Author} to session {SessionId}",
                    message.Id, author, sessionId);

                // Инвалидируем кеш для этой сессии
                await InvalidateSessionCacheAsync(sessionId, cancellationToken);

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving message from {Author} to session {SessionId}", author, sessionId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<ChatMessage>> GetSessionHistoryAsync(
            Guid sessionId,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
            }

            var effectiveLimit = limit ?? DefaultMessageLimit;

            _logger.LogDebug("Getting session history for {SessionId} with limit {Limit}", sessionId, effectiveLimit);

            try
            {
                // Проверяем кеш
                var cacheKey = GetSessionCacheKey(sessionId);
                if (_cache.TryGetValue(cacheKey, out List<ChatMessage>? cachedMessages) && cachedMessages != null)
                {
                    _logger.LogDebug("Found cached messages for session {SessionId}", sessionId);
                    return cachedMessages.Take(effectiveLimit).ToList();
                }

                // Загружаем из базы данных
                var messages = await _context.ChatMessages
                    .Where(m => m.SessionId == sessionId)
                    .OrderBy(m => m.CreatedAt)
                    .Take(effectiveLimit)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("Loaded {MessageCount} messages for session {SessionId} from database",
                    messages.Count, sessionId);

                // Добавляем в кеш с graceful handling
                try
                {
                    _cache.Set(cacheKey, messages, _cacheExpiry);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to cache messages for session {SessionId}, continuing without cache",
                        sessionId);
                }

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session history for {SessionId}", sessionId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<ChatSession>> GetUserSessionsAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            _logger.LogDebug("Getting sessions for user {UserId}", userId);

            try
            {
                // Проверяем кеш
                var cacheKey = GetUserSessionsCacheKey(userId);
                if (_cache.TryGetValue(cacheKey, out List<ChatSession>? cachedSessions) && cachedSessions != null)
                {
                    _logger.LogDebug("Found cached sessions for user {UserId}", userId);
                    return cachedSessions;
                }

                // Загружаем из базы данных
                var sessions = await _context.ChatSessions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.LastMessageAt)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("Loaded {SessionCount} sessions for user {UserId} from database",
                    sessions.Count, userId);

                // Добавляем в кеш с graceful handling
                try
                {
                    _cache.Set(cacheKey, sessions, _cacheExpiry);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to cache sessions for user {UserId}, continuing without cache",
                        userId);
                }

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions for user {UserId}", userId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SessionExistsAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                return false;
            }

            _logger.LogDebug("Checking if session {SessionId} exists", sessionId);

            try
            {
                // Проверяем кеш сначала
                var cacheKey = GetSessionCacheKey(sessionId);
                if (_cache.TryGetValue(cacheKey, out _))
                {
                    _logger.LogDebug("Session {SessionId} found in cache", sessionId);
                    return true;
                }

                // Проверяем в базе данных
                var exists = await _context.ChatSessions
                    .AnyAsync(s => s.Id == sessionId, cancellationToken);

                _logger.LogDebug("Session {SessionId} exists in database: {Exists}", sessionId, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if session {SessionId} exists", sessionId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateSessionTitleAsync(
            Guid sessionId,
            string title,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Title cannot be null or empty", nameof(title));
            }

            _logger.LogDebug("Updating title for session {SessionId} to '{Title}'", sessionId, title);

            try
            {
                var session = await _context.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

                if (session == null)
                {
                    throw new InvalidOperationException($"Chat session with ID {sessionId} not found");
                }

                session.Title = title;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Updated title for session {SessionId} to '{Title}'", sessionId, title);

                // Инвалидируем кеш для этой сессии
                await InvalidateSessionCacheAsync(sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating title for session {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Получает ключ кеша для сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <returns>Ключ кеша</returns>
        private string GetSessionCacheKey(Guid sessionId)
        {
            return $"chat_session_{sessionId}";
        }

        /// <summary>
        /// Получает ключ кеша для сессий пользователя
        /// </summary>
        /// <param name="userId">Идентификатор пользователя</param>
        /// <returns>Ключ кеша</returns>
        private string GetUserSessionsCacheKey(string userId)
        {
            return $"user_sessions_{userId}";
        }

        /// <summary>
        /// Обновляет кеш для указанной сессии
        /// </summary>
        /// <param name="session">Сессия для обновления в кеше</param>
        /// <param name="cancellationToken">Токен отмены</param>
        private async Task UpdateSessionCacheAsync(ChatSession session, CancellationToken cancellationToken)
        {
            try
            {
                // Обновляем кеш сессии
                var sessionCacheKey = GetSessionCacheKey(session.Id);
                _cache.Set(sessionCacheKey, session, _cacheExpiry);

                // Обновляем кеш пользовательских сессий, если есть UserId
                if (!string.IsNullOrWhiteSpace(session.UserId))
                {
                    var userSessionsCacheKey = GetUserSessionsCacheKey(session.UserId);

                    // Загружаем текущие сессии из базы данных для обновления кеша
                    var userSessions = await _context.ChatSessions
                        .Where(s => s.UserId == session.UserId)
                        .OrderByDescending(s => s.LastMessageAt)
                        .ToListAsync(cancellationToken);

                    _cache.Set(userSessionsCacheKey, userSessions, _cacheExpiry);
                }
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(cacheEx, "Failed to update cache for session {SessionId}, continuing without cache",
                    session.Id);
            }
        }

        /// <summary>
        /// Инвалидирует кеш для указанной сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="cancellationToken">Токен отмены</param>
        private async Task InvalidateSessionCacheAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                // Получаем сессию для определения UserId
                var session = await _context.ChatSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

                if (session == null)
                {
                    return;
                }

                // Удаляем из кеша сообщения сессии
                var sessionCacheKey = GetSessionCacheKey(sessionId);
                _cache.Remove(sessionCacheKey);

                // Удаляем из кеша пользовательские сессии, если есть UserId
                if (!string.IsNullOrWhiteSpace(session.UserId))
                {
                    var userSessionsCacheKey = GetUserSessionsCacheKey(session.UserId);
                    _cache.Remove(userSessionsCacheKey);
                }

                _logger.LogDebug("Invalidated cache for session {SessionId}", sessionId);
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(cacheEx, "Failed to invalidate cache for session {SessionId}, continuing without cache",
                    sessionId);
            }
        }
    }
}