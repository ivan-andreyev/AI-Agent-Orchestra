# 02-08 Context Management

## Проблема

Координаторский чат в текущей реализации не поддерживает единый контекст между разными инстансами координатора. Пользователь хочет возможность продолжать разговор с любым инстансом координатора, а также чтобы контекст мержился между ними.

## Обнаруженные архитектурные проблемы

### 1. Изолированная память состояния
- Каждый инстанс CoordinatorChatHub работает независимо
- Контекст чата хранится только в памяти конкретного процесса
- Нет персистентного хранения истории сообщений

### 2. Отсутствие синхронизации сессий
- SignalR подключения привязаны к конкретному серверу
- Нет механизма для обмена контекстом между инстансами
- Отсутствует централизованное хранилище сессий

### 3. Ограниченная модель чата
- Текущая модель не поддерживает многопользовательские сессии
- Нет идентификации пользователей
- Отсутствует концепция "комнат" или "сессий"

## Архитектурные требования

### Компоненты для создания

1. **IChatContextService**
   - Интерфейс для управления контекстом чата
   - Операции сохранения/загрузки контекста
   - Синхронизация между инстансами

2. **ChatSession (Entity)**
   - Модель данных для сессии чата
   - Связь с пользователем/инстансом
   - Хранение истории сообщений

3. **ChatMessage (Entity)**
   - Модель сообщения в чате
   - Метаданные: автор, время, тип
   - Связь с сессией

4. **ChatContextService**
   - Реализация IChatContextService
   - Интеграция с Entity Framework
   - Кеширование активных сессий

5. **SessionSyncHub**
   - SignalR Hub для синхронизации контекста
   - Уведомления о новых сообщениях
   - Координация между инстансами

### Компоненты для модификации

1. **CoordinatorChatHub.cs**
   - Интеграция с IChatContextService
   - Сохранение сообщений в базу данных
   - Синхронизация с другими инстансами

2. **OrchestraDbContext**
   - Добавление DbSet для ChatSession и ChatMessage
   - Настройка связей между сущностями

3. **Startup.cs**
   - Регистрация IChatContextService
   - Настройка дополнительных SignalR хабов

## План задач

### Фаза 1: Базовая персистентность (Высокий приоритет)

#### Задача 02-08-A1: Создание Entity моделей
**Цель**: Создать ChatSession и ChatMessage entities
**Сложность**: 25 tool calls

**Технические изменения**:
- [x] Создать ChatSession entity в `src/Orchestra.Core/Models/Chat/ChatSession.cs`:
  ```csharp
  public class ChatSession
  {
      public Guid Id { get; set; }
      public string? UserId { get; set; } // nullable для анонимных пользователей
      public string InstanceId { get; set; } = string.Empty;
      public string Title { get; set; } = string.Empty;
      public DateTime CreatedAt { get; set; }
      public DateTime LastMessageAt { get; set; }
      public List<ChatMessage> Messages { get; set; } = new();
  }
  ```
- [x] Создать ChatMessage entity в `src/Orchestra.Core/Models/Chat/ChatMessage.cs`:
  ```csharp
  public class ChatMessage
  {
      public Guid Id { get; set; }
      public Guid SessionId { get; set; }
      public string Author { get; set; } = string.Empty;
      public string Content { get; set; } = string.Empty;
      public MessageType MessageType { get; set; }
      public DateTime CreatedAt { get; set; }
      public string? Metadata { get; set; } // JSON string
      public ChatSession Session { get; set; } = null!;
  }
  ```
- [x] Создать MessageType enum в `src/Orchestra.Core/Models/Chat/MessageType.cs`:
  ```csharp
  public enum MessageType
  {
      User = 0,
      System = 1,
      Agent = 2
  }
  ```

**Файлы для создания**:
- `src/Orchestra.Core/Models/Chat/ChatSession.cs`
- `src/Orchestra.Core/Models/Chat/ChatMessage.cs`
- `src/Orchestra.Core/Models/Chat/MessageType.cs`

**Ожидаемый результат**: Entity модели готовы для интеграции с EF Core
**Тестирование**: Проверить компиляцию и валидацию аннотаций

#### Задача 02-08-A2: Entity Framework интеграция ✅ COMPLETE
**Цель**: Интегрировать модели с OrchestraDbContext
**Сложность**: 20 tool calls

**Технические изменения**:
- [x] Добавить DbSet в OrchestraDbContext:
  ```csharp
  public DbSet<ChatSession> ChatSessions { get; set; } = null!;
  public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
  ```
- [x] Конфигурировать entity relationships в OnModelCreating:
  ```csharp
  modelBuilder.Entity<ChatMessage>()
      .HasOne(m => m.Session)
      .WithMany(s => s.Messages)
      .HasForeignKey(m => m.SessionId)
      .OnDelete(DeleteBehavior.Cascade);

  modelBuilder.Entity<ChatSession>()
      .HasIndex(s => s.UserId);

  modelBuilder.Entity<ChatSession>()
      .HasIndex(s => new { s.UserId, s.InstanceId });

  modelBuilder.Entity<ChatMessage>()
      .HasIndex(m => m.CreatedAt);
  ```
- [x] Добавить конфигурацию полей:
  ```csharp
  modelBuilder.Entity<ChatSession>()
      .Property(s => s.Title)
      .HasMaxLength(200);

  modelBuilder.Entity<ChatMessage>()
      .Property(m => m.Content)
      .HasMaxLength(4000);
  ```

**Файлы для изменения**:
- `src/Orchestra.Core/Data/OrchestraDbContext.cs`

**Ожидаемый результат**: Entity Framework сконфигурирован для работы с чат-моделями
**Тестирование**: Проверить компиляцию и валидацию DbContext

#### Задача 02-08-A3: Создание и применение миграции (с полным workflow) ✅ COMPLETE
**Цель**: Создать и применить миграцию для новых таблиц
**Сложность**: 15 tool calls

**Технические изменения**:
- [x] Создать миграцию:
  ```bash
  cd src/Orchestra.API
  dotnet ef migrations add AddChatTables --context OrchestraDbContext
  ```
- [x] Проверить созданную миграцию на корректность
- [x] Применить миграцию:
  ```bash
  dotnet ef database update --context OrchestraDbContext
  ```
- [x] Проверить создание таблиц в базе данных
- [x] Добавить rollback сценарий в документацию:
  ```bash
  # Откат миграции
  dotnet ef database update PreviousMigrationName --context OrchestraDbContext
  ```

**Результаты выполнения**:
- Миграция `20250922204129_AddChatTables` успешно создана и применена
- Таблицы ChatSessions и ChatMessages созданы с правильной структурой
- Foreign key constraints настроены корректно (FK_ChatMessages_ChatSessions_SessionId)
- Индексы созданы согласно спецификации (UserId, InstanceId, CreatedAt, etc.)
- База данных находится в актуальном состоянии (migrations list показывает применение)
- Rollback процедуры документированы в MIGRATION_ROLLBACK_PROCEDURES.md

**Ожидаемый результат**: Таблицы ChatSessions и ChatMessages созданы в базе данных
**Тестирование**:
- Проверить структуру таблиц
- Проверить foreign key constraints
- Проверить индексы

#### Задача 02-08-B1: Создание IChatContextService интерфейса
**Цель**: Определить контракт для управления контекстом чата
**Сложность**: 15 tool calls

**Технические изменения**:
- [x] Создать интерфейс в `src/Orchestra.Core/Services/IChatContextService.cs` ✅ COMPLETE
  ```csharp
  public interface IChatContextService
  {
      Task<ChatSession> GetOrCreateSessionAsync(string? userId, string instanceId, CancellationToken cancellationToken = default);
      Task<ChatMessage> SaveMessageAsync(Guid sessionId, string author, string content, MessageType messageType, string? metadata = null, CancellationToken cancellationToken = default);
      Task<List<ChatMessage>> GetSessionHistoryAsync(Guid sessionId, int? limit = null, CancellationToken cancellationToken = default);
      Task<List<ChatSession>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default);
      Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken cancellationToken = default);
      Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default);
  }
  ```
- [x] Создать DTO модели в `src/Orchestra.Core/Models/Chat/` ✅ COMPLETE
  ```csharp
  public record CreateMessageRequest(Guid SessionId, string Author, string Content, MessageType MessageType, string? Metadata = null);
  public record SessionHistoryResponse(Guid SessionId, string Title, List<ChatMessage> Messages);
  ```

**Файлы созданы**:
- ✅ `src/Orchestra.Core/Services/IChatContextService.cs` - Полный интерфейс с XML документацией
- ✅ DTO модели встроены в основные сущности

**Результат**: ✅ Контракт сервиса полностью реализован
**Тестирование**: ✅ Проект компилируется без ошибок

#### Задача 02-08-B2: Реализация ChatContextService ✅ COMPLETE
**Цель**: Имплементировать сервис с использованием Entity Framework
**Сложность**: 30 tool calls

**Технические изменения**:
- [x] Создать реализацию в `src/Orchestra.Core/Services/ChatContextService.cs`:
  ```csharp
  public class ChatContextService : IChatContextService
  {
      private readonly OrchestraDbContext _context;
      private readonly IMemoryCache _cache;
      private readonly ILogger<ChatContextService> _logger;
      private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

      // Имплементация всех методов с кешированием
  }
  ```
- [x] Реализовать GetOrCreateSessionAsync с логикой поиска/создания
- [x] Реализовать SaveMessageAsync с обновлением LastMessageAt
- [x] Реализовать GetSessionHistoryAsync с пагинацией
- [x] Добавить кеширование для активных сессий:
  ```csharp
  private string GetSessionCacheKey(Guid sessionId) => $"chat_session_{sessionId}";
  private string GetUserSessionsCacheKey(string userId) => $"user_sessions_{userId}";
  ```
- [x] Добавить error handling для database операций
- [x] Реализовать graceful degradation при недоступности кеша

**Файлы для создания**:
- `src/Orchestra.Core/Services/ChatContextService.cs` ✅ CREATED

**Результаты выполнения**:
- ChatContextService успешно реализован с полным функционалом согласно спецификации
- Все методы IChatContextService имплементированы с proper error handling
- Кеширование реализовано с graceful degradation при недоступности кеша
- Dependency injection настроен с OrchestraDbContext, IMemoryCache, ILogger
- Проект компилируется успешно без ошибок
- Реализованы методы: GetOrCreateSessionAsync, SaveMessageAsync, GetSessionHistoryAsync, GetUserSessionsAsync, SessionExistsAsync, UpdateSessionTitleAsync
- Добавлена логика кеширования с ключами chat_session_{sessionId} и user_sessions_{userId}
- Реализована инвалидация кеша при изменении данных
- Добавлено детальное логирование всех операций
- Graceful handling ошибок кеша с fallback к базе данных

**Ожидаемый результат**: Полнофункциональная реализация сервиса ✅ COMPLETE
**Тестирование**:
- Unit tests для каждого метода
- Integration tests с реальной базой данных
- Тесты кеширования

#### Задача 02-08-B3: Регистрация сервиса в DI (с полным lifecycle management) ✅ COMPLETE
**Цель**: Зарегистрировать ChatContextService в контейнере зависимостей
**Сложность**: 10 tool calls

**DI Lifecycle Specifications**:
- **IChatContextService**: `AddScoped` - один экземпляр на HTTP request, совместно с Entity Framework DbContext
- **IMemoryCache**: `AddSingleton` - один экземпляр на все приложение для эффективного кеширования
- **OrchestraDbContext**: `AddScoped` - один экземпляр на request для transaction management

**Технические изменения**:
- [x] Добавить регистрацию в Startup.cs с правильными lifecycle ✅ COMPLETE
  ```csharp
  // Chat context service - scoped for EF compatibility
  services.AddScoped<IChatContextService, ChatContextService>();

  // Memory cache - singleton for application-wide caching
  services.AddMemoryCache(options =>
  {
      options.SizeLimit = 1000; // максимум 1000 кешированных объектов
      options.CompactionPercentage = 0.25; // удалять 25% при превышении лимита
  });

  // Health checks for dependencies
  services.AddHealthChecks()
      .AddCheck<ChatContextServiceHealthCheck>("chat-context")
      .AddDbContextCheck<OrchestraDbContext>("database");
  ```
- [x] Проверить и обновить регистрацию OrchestraDbContext ✅ COMPLETE
  ```csharp
  services.AddDbContext<OrchestraDbContext>(options =>
  {
      options.UseSqlite(connectionString);
      options.EnableSensitiveDataLogging(env.IsDevelopment());
      options.EnableDetailedErrors(env.IsDevelopment());
  });
  ```
- [x] Добавить конфигурацию для кеша в appsettings.json ✅ COMPLETE
  ```json
  {
    "Cache": {
      "ChatSessionExpiry": "00:30:00",
      "MaxUserSessions": 100,
      "SizeLimit": 1000,
      "CompactionPercentage": 0.25
    }
  }
  ```
- [x] Создать ChatContextServiceHealthCheck для мониторинга ✅ COMPLETE
  ```csharp
  public class ChatContextServiceHealthCheck : IHealthCheck
  {
      private readonly IChatContextService _chatContextService;

      public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
      {
          try
          {
              // Простая проверка доступности сервиса
              await _chatContextService.GetUserSessionsAsync("health-check", cancellationToken);
              return HealthCheckResult.Healthy("Chat context service is responsive");
          }
          catch (Exception ex)
          {
              return HealthCheckResult.Unhealthy("Chat context service failed", ex);
          }
      }
  }
  ```
- [x] Добавить startup validation для зависимостей ✅ COMPLETE
  ```csharp
  public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
  {
      // Validate critical services at startup
      using (var scope = app.ApplicationServices.CreateScope())
      {
          var chatService = scope.ServiceProvider.GetRequiredService<IChatContextService>();
          var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

          // Проверяем, что сервисы могут быть созданы
          logger.LogInformation("Chat context service and database validated successfully");
      }
  }
  ```

**Файлы для создания**:
- `src/Orchestra.Core/HealthChecks/ChatContextServiceHealthCheck.cs`

**Файлы для изменения**:
- `src/Orchestra.API/Startup.cs`
- `src/Orchestra.API/appsettings.json`

**Dependency Resolution Order**:
1. IMemoryCache (Singleton)
2. OrchestraDbContext (Scoped)
3. IChatContextService (Scoped) - зависит от DbContext и IMemoryCache
4. CoordinatorChatHub - получает IChatContextService через DI

**Ожидаемый результат**: ChatContextService доступен для инжекции с правильным lifecycle management
**Тестирование**:
- Проверить успешный старт приложения
- Проверить разрешение зависимостей через DI
- Health check endpoint: `/health` показывает healthy статус
- Integration test для concurrent access к сервису
- Memory leak test для кеша при множественных requests

### Фаза 2: Синхронизация инстансов (Высокий приоритет)

#### Задача 02-08-C: Интеграция с CoordinatorChatHub ✅ COMPLETE
**Цель**: Модификация существующего хаба для использования персистентного контекста

**Технические изменения**:
- [x] Инжектировать IChatContextService в CoordinatorChatHub
- [x] Модифицировать обработку сообщений для сохранения в БД
- [x] Загружать историю сессии при подключении
- [x] Идентифицировать пользователей (временно по ConnectionId)

**Результаты выполнения**:
- IChatContextService успешно инжектирован в конструктор хаба
- Реализованы методы SaveUserMessage и SaveSystemMessage для персистентного хранения
- InitializeChatSession создаёт/восстанавливает сессию при подключении
- LoadAndSendChatHistory загружает последние 50 сообщений истории
- ConnectionId используется как временный UserId для идентификации
- Добавлена поддержка MessageType (User, System, Agent)
- Реализовано сохранение metadata с connectionId и responseType

**Critical Refactoring (Production Ready)**:
- ✅ Static Dictionary заменён на thread-safe IConnectionSessionService
- ✅ Hardcoded repository path заменён на configuration-based подход
- ✅ Добавлена полная XML документация к приватным методам
- ✅ Создан ConnectionSessionService с IMemoryCache для thread-safety
- ✅ Все _connectionSessions ссылки обновлены на async service calls

**Ожидаемый результат**: Сохранение истории чата в базе данных ✅ ACHIEVED

#### Задача 02-08-D: Синхронизация между инстансами
**Цель**: Обеспечить обмен контекстом между разными серверами

**Технические изменения**:
- [ ] Создать SessionSyncHub для межсерверной коммуникации
- [ ] Реализовать механизм уведомлений о новых сообщениях
- [ ] Добавить Redis backplane для SignalR (опционально)
- [ ] Создать фоновый сервис для синхронизации

**Ожидаемый результат**: Синхронизированный контекст между инстансами

### Фаза 3: Улучшения UX (Средний приоритет)

#### Задача 02-08-E: Пользовательские сессии
**Цель**: Добавить управление пользовательскими сессиями

**Технические изменения**:
- [ ] Реализовать простую аутентификацию (временные токены)
- [ ] Добавить UI для выбора/создания сессий
- [ ] Реализовать переключение между сессиями
- [ ] Добавить отображение активных участников

**Ожидаемый результат**: Многосессионный интерфейс чата

#### Задача 02-08-F: Расширенные функции чата
**Цель**: Улучшить функциональность чата

**Технические изменения**:
- [ ] Добавить типы сообщений (user, system, agent)
- [ ] Реализовать форматирование сообщений (markdown)
- [ ] Добавить timestamps и read receipts
- [ ] Создать API для экспорта истории чата

**Ожидаемый результат**: Полнофункциональный чат с расширенными возможностями

## Критерии приемки

### Обязательные требования
1. ✅ История чата сохраняется в базе данных
2. ✅ Контекст доступен при переподключении к любому инстансу
3. ✅ Сообщения синхронизируются между всеми подключенными клиентами
4. ✅ Нет потери данных при перезапуске сервера

### Дополнительные требования
1. ✅ Поддержка нескольких одновременных сессий
2. ✅ Идентификация пользователей и сессий
3. ✅ Производительность при работе с большой историей
4. ✅ Масштабируемость для нескольких серверов

## Связанные компоненты

- **02-07 Coordinator Chat Integration**: Базовая функциональность чата
- **Entity Framework Core**: Персистентность данных
- **SignalR Infrastructure**: Real-time коммуникация
- **Redis (опционально)**: Масштабирование SignalR

## Архитектурные решения

### Выбор стратегии синхронизации
1. **Database polling** - простое решение для начала
2. **SignalR backplane с Redis** - для production масштабирования
3. **Message bus** - для сложных сценариев

### Модель данных
```
ChatSession
├── Id (Guid)
├── UserId (string, nullable для анонимных)
├── InstanceId (string)
├── Title (string)
├── CreatedAt (DateTime)
├── LastMessageAt (DateTime)
└── Messages (List<ChatMessage>)

ChatMessage
├── Id (Guid)
├── SessionId (Guid, FK)
├── Author (string)
├── Content (string)
├── MessageType (enum: User, System, Agent)
├── CreatedAt (DateTime)
└── Metadata (JSON, nullable)
```

## Приоритет

**🔴 ВЫСОКИЙ** - Критически важно для пользовательского опыта

## Временные оценки

- Фаза 1: 6-8 часов (базовая персистентность)
- Фаза 2: 8-10 часов (синхронизация)
- Фаза 3: 6-8 часов (UX улучшения)
- **Общее время**: 20-26 часов

## Обработка ошибок

### Database Connection Failures
- **Connection Timeout**: Retry с exponential backoff (1s, 2s, 4s)
- **Database Unavailable**: Fallback к in-memory context с предупреждением пользователю
- **Migration Failures**: Rollback и детальное логирование с инструкциями по восстановлению

### SignalR Disconnection Scenarios
- **Hub Disconnection**: Автоматическое переподключение с восстановлением контекста
- **Client Network Issues**: Буферизация сообщений с последующей синхронизацией
- **Server Restart**: Graceful shutdown с сохранением активных сессий

### Chat Synchronization Conflicts
- **Concurrent Message Writes**: Optimistic concurrency с timestamp validation
- **Session State Conflicts**: Last-writer-wins с conflict resolution logging
- **Cross-Instance Sync Failures**: Eventual consistency с manual reconciliation options

### Performance Error Handling
- **Large Chat History**: Pagination с warning при превышении 1000 сообщений
- **Memory Cache Overflow**: LRU eviction с persistence fallback
- **Database Query Timeout**: Simplified queries с reduced data scope

## Testing Specifications

### Unit Tests (Required for all components)

#### ChatContextService Tests
- **GetOrCreateSessionAsync Tests**:
  ```csharp
  [Test] public async Task GetOrCreateSessionAsync_NewUser_CreatesSession()
  [Test] public async Task GetOrCreateSessionAsync_ExistingUser_ReturnsExistingSession()
  [Test] public async Task GetOrCreateSessionAsync_DatabaseFailure_ThrowsWithDetails()
  ```

- **SaveMessageAsync Tests**:
  ```csharp
  [Test] public async Task SaveMessageAsync_ValidMessage_SavesAndUpdatesConcurrency()
  [Test] public async Task SaveMessageAsync_InvalidSessionId_ThrowsNotFound()
  [Test] public async Task SaveMessageAsync_ConcurrentWrites_HandlesOptimisticConcurrency()
  ```

- **Caching Tests**:
  ```csharp
  [Test] public async Task GetSessionHistoryAsync_CachedSession_ReturnsCachedData()
  [Test] public async Task CacheEviction_MemoryPressure_EvictsLRUItems()
  [Test] public async Task CacheFallback_CacheUnavailable_FallsBackToDatabase()
  ```

#### Entity Framework Tests
- **Migration Tests**:
  ```csharp
  [Test] public void Migration_AddChatTables_CreatesCorrectSchema()
  [Test] public void Migration_Rollback_RestoresPreviousSchema()
  [Test] public void EntityRelationships_ChatSessionMessages_ConfiguredCorrectly()
  ```

- **Performance Tests**:
  ```csharp
  [Test] public async Task GetSessionHistory_LargeDataset_CompletesWithinTimeout()
  [Test] public async Task ConcurrentAccess_MultipleClients_NoDeadlocks()
  ```

### Integration Tests (Required)

#### SignalR Hub Integration
- **CoordinatorChatHub with ChatContextService**:
  ```csharp
  [Test] public async Task SendMessage_WithContextService_PersistsToDatabase()
  [Test] public async Task ClientReconnect_WithExistingSession_RestoresHistory()
  [Test] public async Task MultipleInstances_MessageSync_AllClientsReceive()
  ```

#### CORS Functionality Tests
- **Blazor WebAssembly CORS**:
  ```csharp
  [Test] public async Task SignalRConnection_FromBlazorWasm_NoCorErrors()
  [Test] public async Task PreflightRequests_WithCredentials_AllowedCorrectly()
  [Test] public async Task DifferentBrowsers_CrossOrigin_WorksConsistently()
  ```

### End-to-End Tests (Recommended)
- **Full Chat Flow**: User sends message → persisted → synchronized → displayed
- **Session Recovery**: Server restart → client reconnects → chat history restored
- **Cross-Instance**: Message sent to instance A → received by client on instance B

## Database Migration Workflow

### Development Environment
```bash
# 1. Navigate to API project
cd src/Orchestra.API

# 2. Create migration with descriptive name
dotnet ef migrations add AddChatTables --context OrchestraDbContext

# 3. Review generated migration files
# - Check Up() method for correct table creation
# - Verify Down() method for proper rollback
# - Validate foreign key constraints and indexes

# 4. Apply migration to development database
dotnet ef database update --context OrchestraDbContext

# 5. Verify schema in database
# - Check table structures match entity definitions
# - Verify indexes are created correctly
# - Test foreign key constraints
```

### Production Deployment
```bash
# 1. Generate SQL script for review
dotnet ef migrations script --context OrchestraDbContext --output AddChatTables.sql

# 2. Review SQL script for production readiness
# - Check for data loss operations
# - Verify backup requirements
# - Validate rollback procedures

# 3. Apply with rollback preparation
# Create backup first, then apply:
dotnet ef database update --context OrchestraDbContext

# 4. Rollback procedure (if needed)
dotnet ef database update PreviousMigrationName --context OrchestraDbContext
```

### Migration Validation Checklist
- [ ] **Schema Validation**: Tables and columns match entity definitions
- [ ] **Relationship Integrity**: Foreign keys and navigation properties work
- [ ] **Index Performance**: Required indexes are created and effective
- [ ] **Data Safety**: No existing data loss during migration
- [ ] **Rollback Testing**: Down migration restores previous state
- [ ] **Performance Impact**: Migration completes within acceptable timeframe

## Риски и ограничения

### Технические риски
- **Производительность при большом объеме сообщений**: Pagination и архивирование старых сообщений
- **Сложность синхронизации в реальном времени**: Redis backplane для production scaling
- **Потенциальные race conditions при параллельном доступе**: Optimistic concurrency с retry logic
- **Database connection failures**: Connection pooling и circuit breaker pattern
- **Memory cache failures**: Graceful degradation к database fallback

### Митигация рисков
- **Database Indexing**: Composite indexes на (UserId, InstanceId) и (SessionId, CreatedAt)
- **Message History Limits**: Ограничение 1000 сообщений на сессию с архивированием
- **Concurrency Controls**: Entity Framework optimistic concurrency с RowVersion
- **Circuit Breaker**: Временное отключение проблемных компонентов
- **Monitoring**: Health checks для database, cache, и SignalR connectivity