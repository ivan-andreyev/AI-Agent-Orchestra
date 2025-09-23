# 📋 Технический долг из Фазы 1 - Coordinator Chat Context Management

## Критический долг (требует решения до Фазы 2)

### IChatContextService отсутствует
- **Проблема**: Service layer не реализован
- **Влияние**: Phase 2B заблокирована без service interface
- **Решение**: Создать IChatContextService и ChatContextService
- **Приоритет**: HIGH

### Error handling отсутствует
- **Проблема**: Entity operations не имеют обработки исключений
- **Влияние**: Runtime failures при DB операциях
- **Решение**: Добавить try-catch блоки и graceful degradation
- **Приоритет**: HIGH

### Logging отсутствует
- **Проблема**: Операции с базой данных не логируются
- **Влияние**: Сложность отладки и мониторинга
- **Решение**: Добавить ILogger для DB operations
- **Приоритет**: MEDIUM

## Архитектурные рекомендации

### Dependency Injection lifecycle
- **Рассмотрение**: Scoped vs Singleton для ChatContext
- **Текущее**: Scoped через DbContext
- **Рекомендация**: Сохранить Scoped для thread safety

### Performance оптимизация
- **Потенциал**: Кэширование для частых запросов
- **Кандидаты**: GetSessionsByUserId, GetRecentMessages
- **Технология**: IMemoryCache или Redis

### Security валидация
- **Риск**: Cross-user access к чатам
- **Решение**: Валидация UserId в service layer
- **Имплементация**: Authorization middleware

## Минорный долг (можно решить позже)

### Code coverage
- **Проблема**: Unit тесты для Entity models отсутствуют
- **Решение**: Добавить тесты для ChatSession/ChatMessage validation
- **Приоритет**: LOW

### Documentation
- **Проблема**: API documentation не обновлена
- **Решение**: Обновить OpenAPI specs для новых endpoints
- **Приоритет**: LOW

### Monitoring
- **Возможность**: Метрики для операций с чатом
- **Решение**: Добавить counters для message creation/retrieval
- **Приоритет**: LOW

## Трекинг долга

**Создан**: 2025-09-23
**Phase**: 1 -> 2 transition
**Review score impact**: -15% от максимального scores
**Estimated effort**: 2-3 дня на критический долг

## Следующие шаги

1. **Документировать в backlog**: Добавить items в project backlog
2. **Приоритизировать**: Критический долг должен быть решен до Phase 2B
3. **Планировать**: Включить в следующий sprint/iteration