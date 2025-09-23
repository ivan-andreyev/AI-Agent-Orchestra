using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchestra.Core.Models.Chat;
using System;
using System.Threading.Tasks;

namespace Orchestra.Core.Services
{
    /// <summary>
    /// Thread-safe реализация сервиса управления сессиями чата через IMemoryCache
    /// Обеспечивает безопасное хранение сессий для SignalR connections
    /// </summary>
    public class ConnectionSessionService : IConnectionSessionService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ConnectionSessionService> _logger;
        private readonly TimeSpan _sessionExpiry = TimeSpan.FromHours(24);

        /// <summary>
        /// Конструктор сервиса управления сессиями
        /// </summary>
        /// <param name="cache">Кеш для хранения сессий</param>
        /// <param name="logger">Логгер для отслеживания операций</param>
        public ConnectionSessionService(
            IMemoryCache cache,
            ILogger<ConnectionSessionService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<ChatSession?> GetSessionAsync(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
            }

            var cacheKey = GetCacheKey(connectionId);

            if (_cache.TryGetValue<ChatSession>(cacheKey, out var session))
            {
                _logger.LogDebug("Session found in cache for connection {ConnectionId}", connectionId);
                return Task.FromResult<ChatSession?>(session);
            }

            _logger.LogDebug("No session found for connection {ConnectionId}", connectionId);
            return Task.FromResult<ChatSession?>(null);
        }

        /// <inheritdoc />
        public Task SetSessionAsync(string connectionId, ChatSession session)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var cacheKey = GetCacheKey(connectionId);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(_sessionExpiry)
                .SetPriority(CacheItemPriority.Normal)
                .SetSize(1);

            _cache.Set(cacheKey, session, cacheOptions);
            _logger.LogDebug("Session cached for connection {ConnectionId} with {Expiry} expiry",
                connectionId, _sessionExpiry);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> RemoveSessionAsync(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
            }

            var cacheKey = GetCacheKey(connectionId);

            if (_cache.TryGetValue<ChatSession>(cacheKey, out _))
            {
                _cache.Remove(cacheKey);
                _logger.LogDebug("Session removed for connection {ConnectionId}", connectionId);
                return Task.FromResult(true);
            }

            _logger.LogDebug("No session to remove for connection {ConnectionId}", connectionId);
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public Task<bool> HasSessionAsync(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
            }

            var cacheKey = GetCacheKey(connectionId);
            var exists = _cache.TryGetValue<ChatSession>(cacheKey, out _);

            _logger.LogDebug("Session exists check for connection {ConnectionId}: {Exists}",
                connectionId, exists);

            return Task.FromResult(exists);
        }

        /// <summary>
        /// Генерирует ключ кеша для connection ID
        /// </summary>
        /// <param name="connectionId">Идентификатор подключения</param>
        /// <returns>Ключ для кеша</returns>
        private string GetCacheKey(string connectionId) => $"connection_session_{connectionId}";
    }
}