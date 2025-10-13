# 🤖 Спецификации автономных суб-агентов

Этот каталог содержит технические спецификации суб-агентов для системы распределённой автономной работы Claude Code.

## 📚 Структура спецификаций

Каждый файл `.md` агента содержит:

1. **Frontmatter** (metadata для Claude Code)
   ```yaml
   ---
   name: agent-name
   description: "краткое описание"
   tools: Bash, Glob, Grep, LS, Read, Write, Edit, TodoWrite
   model: opus
   color: purple
   ---
   ```

2. **Назначение** - для чего создан агент

3. **Инструменты** - какие tools агент использует

4. **Workflow** - пошаговый алгоритм работы

5. **Автоматические рекомендации** - какие агенты вызывать дальше

6. **Примеры использования**

7. **Интеграция** - как агент взаимодействует с существующими агентами

## 🎯 Приоритеты внедрения

### 🔴 P0 (Критические) - Фаза 1 (1-2 недели)
- ✅ **systematic-plan-reviewer** - автоматизация systematic review (3-5 дней)
- 📝 **plan-readiness-validator** - LLM готовность планов (5-7 дней)
- 📝 **review-consolidator** - армия ревьюеров координатор (4-6 дней)

### 🟡 P1 (Важные) - Фаза 2 (2-3 недели)
- 📝 **git-workflow-manager** - безопасный git workflow (4-6 дней)
- 📝 **dependency-analyzer** - анализ зависимостей (5-7 дней)

### 🟢 P2 (Аналитические) - Фаза 3 (3-4 недели)
- 📝 **performance-profiler** - профилирование производительности (6-8 дней)
- 📝 **documentation-synchronizer** - синхронизация документации (5-7 дней)

## 📋 Список файлов

| Файл | Агент | Приоритет | Статус |
|------|-------|-----------|--------|
| `systematic-plan-reviewer.md` | Systematic Plan Reviewer | 🔴 P0 | ✅ Создан |
| `plan-readiness-validator.md` | Plan Readiness Validator | 🔴 P0 | 📝 TODO |
| `review-consolidator.md` | Review Consolidator | 🔴 P0 | 📝 TODO |
| `git-workflow-manager.md` | Git Workflow Manager | 🟡 P1 | 📝 TODO |
| `dependency-analyzer.md` | Dependency Analyzer | 🟡 P1 | 📝 TODO |
| `performance-profiler.md` | Performance Profiler | 🟢 P2 | 📝 TODO |
| `documentation-synchronizer.md` | Documentation Synchronizer | 🟢 P2 | 📝 TODO |

## 🔗 Связанные файлы

- **Архитектура:** [AGENTS_ARCHITECTURE.md](AGENTS_ARCHITECTURE.md)
- **Анализ экосистемы:** [../docs/AGENTS_ECOSYSTEM_ANALYSIS.md](../docs/AGENTS_ECOSYSTEM_ANALYSIS.md)
- **Шаблон агента:** [templates/agent-template.md](templates/agent-template.md)
- **Интеграция:** [../CLAUDE.md](../CLAUDE.md)

## 🚀 Использование

### Для внедрения нового агента:

1. Изучить архитектуру в [AGENTS_ARCHITECTURE.md](AGENTS_ARCHITECTURE.md)
2. Скопировать шаблон из [templates/agent-template.md](templates/agent-template.md)
3. Заполнить все секции согласно спецификации
4. Определить CRITICAL/RECOMMENDED/OPTIONAL рекомендации
5. Протестировать на реальных сценариях
6. Обновить матрицу переходов в AGENTS_ARCHITECTURE.md

### Для использования агента:

```typescript
// Через Task tool
Task({
  subagent_type: "systematic-plan-reviewer",
  description: "Validate plan structure",
  prompt: "Analyze plan structure for violations..."
})
```

## 📊 Ожидаемые метрики

После внедрения всех агентов:
- ⏱️ **Review time:** 1.5-2.5 часа → 10-15 минут (10-15x)
- 🤖 **Автоматизация:** 50% → 90% (+40%)
- ✅ **Качество:** пропуск этапов 20-30% → <5% (4-6x)
- 📈 **LLM Readiness:** не измеряется → ≥90% гарантированно

---

**См. также:** [Архитектура агентов](AGENTS_ARCHITECTURE.md)
