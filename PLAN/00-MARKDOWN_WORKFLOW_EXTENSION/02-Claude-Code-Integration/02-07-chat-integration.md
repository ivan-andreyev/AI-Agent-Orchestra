# 02-07 Coordinator Chat Integration

## Проблема

Координаторский чат не работает в Blazor WebAssembly UI из-за проблем с CORS и жестко заданными URL. Пользователь видит интерфейс чата, но не может отправлять сообщения или получать ответы.

## Обнаруженные технические проблемы

### 1. CORS Configuration
- CoordinatorChat.razor подключается к SignalR хабу на `http://localhost:5002`
- Требуется правильная настройка CORS для работы с Blazor WebAssembly

### 2. Hardcoded URLs
- В CoordinatorChat.razor строка 150: `.WithUrl("http://localhost:5002/coordinatorHub")`
- Необходимо динамическое определение URL базируясь на конфигурации

### 3. Repository Path Configuration
- В CoordinatorChatHub.cs строка 75: жестко задан путь `@"C:\Users\mrred\RiderProjects\AI-Agent-Orchestra"`
- Требуется конфигурируемый путь к репозиторию

## Архитектурные требования

### Компоненты для модификации

1. **CoordinatorChat.razor**
   - Исправить подключение к SignalR Hub
   - Добавить динамическое определение URL
   - Улучшить обработку ошибок подключения

2. **CoordinatorChatHub.cs**
   - Убрать hardcoded repository path
   - Добавить dependency injection для конфигурации
   - Улучшить error handling

3. **Startup.cs**
   - Проверить CORS конфигурацию для Blazor WebAssembly
   - Добавить конфигурацию для repository paths

## План задач

### Фаза 1: Исправление CORS и URL (Высокий приоритет)

#### Задача 02-07-A1: Конфигурация SignalR URL в Blazor
**Цель**: Заменить hardcoded URL на конфигурируемый
**Сложность**: 15 tool calls

**Технические изменения**:
- [ ] Добавить SignalR конфигурацию в appsettings.json:
  ```json
  {
    "SignalR": {
      "HubUrl": "https://localhost:5002/coordinatorHub",
      "FallbackUrl": "http://localhost:5002/coordinatorHub"
    }
  }
  ```
- [ ] Инжектировать IConfiguration в CoordinatorChat.razor
- [ ] Заменить hardcoded URL `.WithUrl("http://localhost:5002/coordinatorHub")` на `config["SignalR:HubUrl"]`
- [ ] Добавить fallback логику для определения URL

**Файлы для изменения**:
- `src/Orchestra.Web/Components/CoordinatorChat.razor` (строка 150)
- `src/Orchestra.API/appsettings.json`
- `src/Orchestra.API/appsettings.Development.json`

**Ожидаемый результат**: SignalR подключение использует конфигурируемый URL
**Тестирование**: Проверить подключение с разными URL в конфигурации

#### Задача 02-07-A2: Улучшение обработки SignalR подключений
**Цель**: Добавить retry логику и улучшить error handling
**Сложность**: 20 tool calls

**Технические изменения**:
- [ ] Добавить retry механизм для SignalR подключений:
  ```csharp
  connection.WithAutomaticReconnect(new[] {
    TimeSpan.Zero, TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
  })
  ```
- [ ] Добавить обработку событий подключения/отключения
- [ ] Реализовать graceful handling для network errors
- [ ] Добавить логирование состояний подключения

**Файлы для изменения**:
- `src/Orchestra.Web/Components/CoordinatorChat.razor`

**Ожидаемый результат**: Стабильные подключения с автоматическим переподключением
**Тестирование**:
- Тест обрыва соединения
- Тест недоступности сервера
- Тест автоматического переподключения

#### Задача 02-07-A3: UI feedback для состояний подключения
**Цель**: Показать пользователю состояние SignalR подключения
**Сложность**: 10 tool calls

**Технические изменения**:
- [ ] Добавить индикатор состояния подключения в UI:
  ```razor
  <div class="connection-status @connectionStatusClass">
    <i class="@connectionIcon"></i>
    @connectionStatusText
  </div>
  ```
- [ ] Добавить CSS стили для разных состояний (Connected, Connecting, Disconnected)
- [ ] Обновлять UI при изменении состояния подключения
- [ ] Добавить уведомления об ошибках подключения

**Файлы для изменения**:
- `src/Orchestra.Web/Components/CoordinatorChat.razor`
- `src/Orchestra.Web/wwwroot/css/components.css`

**Ожидаемый результат**: Пользователь видит текущее состояние подключения
**Тестирование**: Проверить отображение всех состояний подключения

#### Задача 02-07-B: CORS конфигурация для Blazor WebAssembly
**Цель**: Обеспечить правильную работу cross-origin запросов
**Сложность**: 15 tool calls

**Технические изменения**:
- [ ] Обновить CORS политику в Startup.cs с Blazor WebAssembly специфичными настройками:
  ```csharp
  services.AddCors(options =>
  {
      options.AddPolicy("BlazorWasmPolicy", builder =>
      {
          builder
              .WithOrigins("https://localhost:5001", "http://localhost:5000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetIsOriginAllowedToReturnTrue();
      });
  });
  ```
- [ ] Добавить специфичные headers для SignalR:
  ```csharp
  .WithHeaders("Authorization", "Content-Type", "x-signalr-user-agent")
  ```
- [ ] Применить CORS политику к SignalR endpoints
- [ ] Добавить конфигурацию для production окружения

**Файлы для изменения**:
- `src/Orchestra.API/Startup.cs`
- `src/Orchestra.API/appsettings.json` (добавить CORS origins)

**Ожидаемый результат**: Нет CORS ошибок при подключении к SignalR
**Тестирование**:
- Проверить подключение из Blazor WebAssembly
- Проверить работу с разными browsers
- Проверить preflight requests

### Фаза 2: Улучшение конфигурации (Средний приоритет)

#### Задача 02-07-C1: Создание сервиса для путей репозитория (с DI lifecycle)
**Цель**: Создать абстракцию для управления путями
**Сложность**: 20 tool calls

**DI Lifecycle Specifications**:
- **IRepositoryPathService**: `AddSingleton` - не содержит состояния, может быть разделен между всеми requests
- **IConfiguration**: Framework-managed Singleton - автоматически инжектируется
- **ILogger<RepositoryPathService>**: Framework-managed Singleton - auto-injected

**Технические изменения**:
- [ ] Создать интерфейс IRepositoryPathService:
  ```csharp
  public interface IRepositoryPathService
  {
      string GetRepositoryBasePath();
      string GetFullPath(string relativePath);
      bool ValidateRepositoryPath(string path);
      bool IsPathAllowed(string relativePath);
  }
  ```
- [ ] Реализовать RepositoryPathService с конфигурацией:
  ```csharp
  public class RepositoryPathService : IRepositoryPathService
  {
      private readonly IConfiguration _configuration;
      private readonly ILogger<RepositoryPathService> _logger;
      private readonly string _basePath;
      private readonly string[] _allowedPaths;

      public RepositoryPathService(IConfiguration configuration, ILogger<RepositoryPathService> logger)
      {
          _configuration = configuration;
          _logger = logger;
          _basePath = _configuration["Repository:BasePath"] ?? throw new InvalidOperationException("Repository:BasePath not configured");
          _allowedPaths = _configuration.GetSection("Repository:AllowedPaths").Get<string[]>() ?? Array.Empty<string>();
      }
      // Implementation with validation and fallbacks
  }
  ```
- [ ] Добавить конфигурацию в appsettings.json:
  ```json
  {
    "Repository": {
      "BasePath": "C:/Projects/AI-Agent-Orchestra",
      "AllowedPaths": ["PLAN", "src", "docs"],
      "ValidateAccess": true
    }
  }
  ```
- [ ] Зарегистрировать сервис в DI как Singleton с validation:
  ```csharp
  services.AddSingleton<IRepositoryPathService>(provider =>
  {
      var config = provider.GetRequiredService<IConfiguration>();
      var logger = provider.GetRequiredService<ILogger<RepositoryPathService>>();

      var service = new RepositoryPathService(config, logger);

      // Проверяем конфигурацию при создании
      var basePath = service.GetRepositoryBasePath();
      if (!Directory.Exists(basePath))
      {
          throw new DirectoryNotFoundException($"Repository base path not found: {basePath}");
      }

      logger.LogInformation("Repository path service initialized with base path: {BasePath}", basePath);
      return service;
  });
  ```

**Файлы для создания**:
- `src/Orchestra.Core/Services/IRepositoryPathService.cs`
- `src/Orchestra.Core/Services/RepositoryPathService.cs`

**Файлы для изменения**:
- `src/Orchestra.API/Startup.cs`
- `src/Orchestra.API/appsettings.json`

**Dependency Resolution Order**:
1. IConfiguration (Framework Singleton)
2. ILogger<RepositoryPathService> (Framework Singleton)
3. IRepositoryPathService (Custom Singleton) - зависит от Configuration и Logger
4. CoordinatorChatHub - получает IRepositoryPathService через DI

**Ожидаемый результат**: Сервис для управления путями расшарен как Singleton
**Тестирование**:
- Unit tests для валидации путей и конфигурации
- Integration test для DI resolution и singleton behavior
- Startup validation test - проверяем ошибку при неправильном пути

#### Задача 02-07-C2: Интеграция сервиса в CoordinatorChatHub
**Цель**: Заменить hardcoded path на использование сервиса
**Сложность**: 10 tool calls

**Технические изменения**:
- [ ] Инжектировать IRepositoryPathService в CoordinatorChatHub
- [ ] Заменить hardcoded path `@"C:\Users\mrred\RiderProjects\AI-Agent-Orchestra"` на вызов сервиса
- [ ] Добавить валидацию путей перед использованием
- [ ] Добавить error handling для недоступных путей

**Файлы для изменения**:
- `src/Orchestra.API/Hubs/CoordinatorChatHub.cs` (строка 75)

**Ожидаемый результат**: CoordinatorChatHub использует конфигурируемые пути
**Тестирование**:
- Тест с разными базовыми путями
- Тест валидации недоступных путей
- Integration test с полной цепочкой вызовов

#### Задача 02-07-D: UI/UX улучшения
**Цель**: Улучшить пользовательский опыт чата

**Технические изменения**:
- [ ] Добавить индикаторы состояния подключения
- [ ] Реализовать auto-scroll для сообщений
- [ ] Добавить loading states для отправки сообщений
- [ ] Улучшить error messaging

**Ожидаемый результат**: Более удобный и информативный интерфейс чата

## Критерии приемки

### Обязательные требования
1. ✅ Пользователь может отправлять сообщения через Blazor UI
2. ✅ Сообщения успешно доставляются к Claude Code агенту
3. ✅ Ответы от агента отображаются в реальном времени
4. ✅ Нет CORS ошибок в браузерной консоли

### Дополнительные требования
1. ✅ Система работает с конфигурируемыми путями
2. ✅ Есть четкая обратная связь о состоянии подключения
3. ✅ Graceful handling сетевых ошибок

## Связанные компоненты

- **02-08 Context Management**: Управление контекстом между инстансами
- **02-Claude-Code-Integration**: Основная интеграция с Claude Code
- **SignalR Infrastructure**: Система real-time коммуникации

## Приоритет

**🔴 ВЫСОКИЙ** - Блокирует пользователя от использования chat функциональности

## Обработка ошибок

### SignalR Connection Failures
- **Network Connectivity Issues**: Автоматическое переподключение с exponential backoff
- **Server Unavailable**: Fallback UI с сообщением о недоступности сервиса
- **CORS Errors**: Детальные сообщения об ошибках с ссылками на решение
- **Authentication Failures**: Redirect к login с preserved контекстом

### Configuration Errors
- **Missing SignalR URL**: Default fallback к localhost с warning message
- **Invalid Repository Path**: User-friendly error с инструкциями по настройке
- **Permission Denied**: Clear messaging о необходимых правах доступа

### UI Error States
- **Connection Status Indicators**: Connected (green), Connecting (yellow), Disconnected (red)
- **Message Send Failures**: Retry button с queue buffering
- **Timeout Handling**: Progress indicators с cancel options

## Testing Specifications

### Unit Tests (Required)

#### SignalR Configuration Tests
```csharp
[Test] public void SignalRConfig_ValidUrl_ConnectsSuccessfully()
[Test] public void SignalRConfig_InvalidUrl_FallsBackToDefault()
[Test] public void SignalRConfig_MissingConfig_ShowsUserFriendlyError()
```

#### CORS Functionality Tests
```csharp
[Test] public async Task CorsPolicy_BlazorOrigins_AllowsConnection()
[Test] public async Task CorsPolicy_UnauthorizedOrigin_BlocksConnection()
[Test] public async Task CorsPolicy_WithCredentials_WorksCorrectly()
[Test] public async Task PreflightRequests_SignalRHeaders_Allowed()
```

#### Repository Path Service Tests
```csharp
[Test] public void RepositoryPath_ValidPath_ReturnsCorrectPath()
[Test] public void RepositoryPath_InvalidPath_ThrowsValidationError()
[Test] public void RepositoryPath_RelativePath_ConvertsToAbsolute()
[Test] public void RepositoryPath_PermissionDenied_ShowsHelpfulError()
```

### Integration Tests (Required)

#### End-to-End Chat Flow
```csharp
[Test] public async Task ChatFlow_SendMessage_ReceivesResponse()
[Test] public async Task ChatFlow_ServerRestart_ReconnectsAutomatically()
[Test] public async Task ChatFlow_NetworkInterruption_BuffersAndSyncs()
```

#### CORS Integration Tests
```csharp
[Test] public async Task BlazorClient_SignalRConnection_NoCorErrors()
[Test] public async Task BlazorClient_DifferentBrowsers_ConsistentBehavior()
[Test] public async Task BlazorClient_ProductionOrigins_WorksInDeployment()
```

## Configuration Schema

### appsettings.json Structure
```json
{
  "SignalR": {
    "HubUrl": "https://localhost:5002/coordinatorHub",
    "FallbackUrl": "http://localhost:5002/coordinatorHub",
    "ConnectionTimeout": "00:01:00",
    "RetryDelays": ["00:00:01", "00:00:02", "00:00:05", "00:00:10"]
  },
  "Repository": {
    "BasePath": "C:/Projects/AI-Agent-Orchestra",
    "AllowedPaths": ["PLAN", "src", "docs"],
    "ValidateAccess": true
  },
  "Cors": {
    "BlazorOrigins": [
      "https://localhost:5001",
      "http://localhost:5000",
      "https://yourdomain.com"
    ],
    "AllowCredentials": true,
    "AllowedHeaders": ["Authorization", "Content-Type", "x-signalr-user-agent"]
  }
}
```

### Environment-Specific Configurations

#### appsettings.Development.json
```json
{
  "SignalR": {
    "HubUrl": "http://localhost:5002/coordinatorHub"
  },
  "Cors": {
    "BlazorOrigins": ["http://localhost:5000", "https://localhost:5001"]
  }
}
```

#### appsettings.Production.json
```json
{
  "SignalR": {
    "HubUrl": "https://api.yourdomain.com/coordinatorHub"
  },
  "Cors": {
    "BlazorOrigins": ["https://yourdomain.com"]
  }
}
```

## Временные оценки

- Фаза 1: 4-6 часов (критический блокер)
- Фаза 2: 3-4 часа (улучшения)
- **Общее время**: 7-10 часов