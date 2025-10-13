# 🏗️ Архитектура автономных суб-агентов

Архитектурная документация системы распределённых автономных суб-агентов для Claude Code.

**Основано на анализе:** [../docs/AGENTS_ECOSYSTEM_ANALYSIS.md](../docs/AGENTS_ECOSYSTEM_ANALYSIS.md)

---

## 🎯 Архитектурное видение

### Ключевая проблема
Разработка сложного ПО требует множества последовательных этапов (планирование → валидация → реализация → тестирование → ревью → документирование → коммит). Разрывы между этапами приводят к:
- Пропуску критичных шагов (20-30% случаев)
- Длительным ручным ревью (1.5-2.5 часа)
- Отсутствию метрик качества (LLM Readiness, Coverage)
- Необходимости постоянного участия пользователя

### Архитектурное решение
**Распределённая самоорганизующаяся система автономных агентов** с встроенными рекомендациями переходов.

**НЕ централизованная оркестрация** (workflow-orchestrator + workflows.yaml), **А распределённая координация** через cross-recommendations между агентами.

## 🏛️ Архитектурные принципы

### 1. Распределённая самоорганизация (Distributed Self-Organization)

**Принцип:** Каждый агент самостоятельно определяет, какие агенты должны быть вызваны после его завершения.

**Реализация:**
```markdown
## 🔄 АВТОМАТИЧЕСКИЕ РЕКОМЕНДАЦИИ

### При успешном завершении:
**CRITICAL:**
- **work-plan-reviewer**: Validate plan structure
  - Condition: Always after plan creation
  - Reason: Ensure plan follows standards

**RECOMMENDED:**
- **parallel-plan-optimizer**: Optimize for parallel execution
  - Condition: Plan has >5 tasks
  - Reason: Reduce execution time by 40-50%
```

**Преимущества:**
- ✅ Нет single point of failure (централизованного orchestrator)
- ✅ Агенты эволюционируют независимо
- ✅ Легко добавлять новых агентов
- ✅ Естественная fault tolerance

### 2. Автономное продление работы (Autonomous Work Extension)

**Принцип:** Агенты рекомендуют следующие шаги в формате, который Claude Code может автоматически интерпретировать и выполнять.

**Формат рекомендаций:**
```
✅ [Agent Name] completed successfully

[Результаты работы]

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: [agent-name]
   Reason: [почему обязательно]
   Command: Use Task tool with subagent_type: "[agent-name]"
   Parameters: [key parameters]

2. ⚠️ RECOMMENDED: [agent-name]
   Reason: [почему рекомендуется]
   Command: Use Task tool with subagent_type: "[agent-name]"

3. 💡 OPTIONAL: [agent-name]
   Reason: [когда полезно]
   Condition: [условие активации]
```

**Уровни приоритета:**
- 🚨 **CRITICAL** - обязательные следующие шаги (блокируют workflow без выполнения)
- ⚠️ **RECOMMENDED** - рекомендуемые шаги (улучшают качество)
- 💡 **OPTIONAL** - условные шаги (зависят от контекста)

### 3. Built-in Workflow Patterns

**Принцип:** Workflows встроены в агентов через рекомендации, а не определены в централизованном конфиге.

**Примеры встроенных workflow:**

#### Feature Development Pipeline
```
work-plan-architect → work-plan-reviewer → systematic-plan-reviewer →
plan-readiness-validator → plan-task-executor → plan-review-iterator →
plan-task-completer → test-healer →
code-principles-reviewer (parallel) + code-style-reviewer (parallel) →
architecture-documenter → pre-completion-validator → git-workflow-manager
```

#### Bug Fix Pipeline (TDD)
```
test-healer (create failing test) → plan-task-executor (fix bug) →
plan-review-iterator → plan-task-completer →
test-healer (green tests) → test-healer (regression) →
code-principles-reviewer + code-style-reviewer →
pre-completion-validator → git-workflow-manager
```

#### Refactoring Pipeline
```
work-plan-architect → test-healer (baseline 100%) →
dependency-analyzer → plan-task-executor → plan-review-iterator →
plan-task-completer → test-healer (incremental) →
performance-profiler → code-principles-reviewer + code-style-reviewer →
architecture-documenter → pre-completion-validator → git-workflow-manager
```

**Реализация:** Каждый агент в цепочке рекомендует следующего через CRITICAL/RECOMMENDED/OPTIONAL.

### 4. Conditional Recommendations

**Принцип:** Рекомендации зависят от контекста выполнения и результатов работы.

**Примеры условий:**

```markdown
### Conditional recommendations:

- IF plan.tasks > 5 THEN recommend **parallel-plan-optimizer**
  - Reason: Large plans benefit from parallel execution

- IF plan.has_architecture_changes THEN recommend **architecture-documenter**
  - Reason: Document architectural decisions

- IF test_failures.contains("DI issues") THEN recommend **code-principles-reviewer**
  - Reason: Validate Dependency Injection architecture

- IF refactoring = true THEN recommend **performance-profiler**
  - Reason: Ensure no performance degradation
```

### 5. Explicit Tool Requirements

**Принцип:** Каждый агент явно декларирует используемые tools в frontmatter для Claude Code.

**Формат frontmatter:**
```yaml
---
name: systematic-plan-reviewer
description: "Automated plan structure validation using PowerShell scripts"
tools: Bash, Read, Glob, Grep, TodoWrite
model: opus
color: blue
---
```

**Обязательные поля:**
- `name` (kebab-case) - уникальный идентификатор агента
- `description` - краткое описание (1-2 предложения или examples)
- `tools` - inline comma-separated list: `Tool1, Tool2, Tool3`

**Опциональные поля:**
- `model` - opus, sonnet, haiku (предпочтительная модель для агента)
- `color` - цвет агента для визуализации (blue, green, orange, red и т.д.)

**Примечание:** Поля `priority` и `estimated_implementation` были удалены из всех агентов как галлюцинированные (не поддерживаются официальной спецификацией Claude Code)

## 📊 Матрица переходов агентов

Таблица показывает CRITICAL и RECOMMENDED переходы между агентами:

| Агент | CRITICAL recommendations | RECOMMENDED recommendations |
|-------|--------------------------|----------------------------|
| **work-plan-architect** | work-plan-reviewer<br>architecture-documenter (if arch changes) | parallel-plan-optimizer (>5 tasks)<br>plan-readiness-validator |
| **work-plan-reviewer** | work-plan-architect (if violations) | systematic-plan-reviewer<br>architecture-documenter |
| **systematic-plan-reviewer** | work-plan-architect (if critical violations) | plan-readiness-validator<br>architecture-documenter |
| **plan-readiness-validator** | work-plan-architect (if <90% score) | plan-task-executor (if ≥90%) |
| **plan-task-executor** | plan-review-iterator (always after execution) | - |
| **plan-review-iterator** | plan-task-completer (if reviews satisfied 80%+) | Self-iteration (if issues found, max 2) |
| **plan-task-completer** | work-plan-reviewer (always)<br>plan-task-executor (next task) | parallel-plan-optimizer (if ≥3 ready tasks)<br>architecture-documenter (if arch change) |
| **test-healer** | pre-completion-validator (if 100%) | code-principles-reviewer (if DI issues) |
| **code-principles-reviewer** | code-style-reviewer (parallel) | architecture-documenter (if violations) |
| **code-style-reviewer** | code-principles-reviewer (parallel) | - |
| **architecture-documenter** | - | work-plan-architect (if redesign needed) |
| **parallel-plan-optimizer** | plan-task-executor | architecture-documenter |
| **pre-completion-validator** | work-plan-reviewer (if mismatches)<br>git-workflow-manager (if OK) | - |
| **git-workflow-manager** | pre-completion-validator (before commit) | test-healer (if tests not run) |

## 🔧 Структура агента

### Стандартные секции спецификации

Каждый агент должен содержать следующие секции (в порядке):

```markdown
---
[frontmatter - см. раздел "Explicit Tool Requirements"]
---

# [Agent Name] Agent

## 🎯 НАЗНАЧЕНИЕ
Чёткое определение проблемы и решения.

## 🛠️ ИНСТРУМЕНТЫ
Список tools с пояснением использования.

## 📋 WORKFLOW
Пошаговый алгоритм работы агента.

## 🔄 АВТОМАТИЧЕСКИЕ РЕКОМЕНДАЦИИ
### При успешном завершении:
**CRITICAL:** [обязательные шаги]
**RECOMMENDED:** [рекомендуемые шаги]

### При обнаружении проблем:
**CRITICAL:** [как исправлять]

### Conditional recommendations:
IF-THEN правила условных рекомендаций

### Example output:
Пример финального вывода с рекомендациями

## 📊 МЕТРИКИ УСПЕХА
Измеримые показатели эффективности.

## 🔗 ИНТЕГРАЦИЯ
Связи с существующими агентами и правилами.

## 🧪 ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
Реальные сценарии применения.

## ⚠️ ОСОБЫЕ СЛУЧАИ
Edge cases и failure scenarios.
```

### Шаблон агента

Универсальный шаблон доступен в [templates/agent-template.md](templates/agent-template.md).

## 🎯 Приоритизация внедрения

### 🔴 P0 (Критические) - Фаза 1 (1-2 недели)

**Критерии P0:**
- Устраняют критические пробелы в существующих pipelines
- Блокируют автоматизацию без них
- Дают немедленный эффект (5-10x ускорение)

**Агенты:**
1. **systematic-plan-reviewer** (3-5 дней)
   - Автоматизирует systematic review через PowerShell скрипты
   - Заменяет 30-60 минут ручной работы на 1-2 минуты

2. **plan-readiness-validator** (5-7 дней)
   - Оценивает LLM готовность планов (≥90% score)
   - Предотвращает провальные попытки исполнения

3. **review-consolidator** (4-6 дней)
   - Координирует параллельный запуск армии ревьюеров
   - Консолидирует результаты в единый отчёт

### 🟡 P1 (Важные) - Фаза 2 (2-3 недели)

**Критерии P1:**
- Улучшают существующие workflow
- Добавляют важные функции безопасности
- Повышают качество на 30-50%

**Агенты:**
4. **git-workflow-manager** (4-6 дней)
   - Безопасный git commit/push/PR workflow
   - Предотвращает случайные коммиты и force push

5. **dependency-analyzer** (5-7 дней)
   - Анализ зависимостей при рефакторинге
   - Обнаружение vulnerabilities

### 🟢 P2 (Аналитические) - Фаза 3 (3-4 недели)

**Критерии P2:**
- Аналитика и оптимизация
- Долгосрочная поддержка качества
- Профилактика технического долга

**Агенты:**
6. **performance-profiler** (6-8 дней)
   - Профилирование производительности
   - Регрессионные тесты при рефакторинге

7. **documentation-synchronizer** (5-7 дней)
   - Синхронизация код↔документация
   - Обнаружение устаревших секций

## 🚀 Интеграция с Claude Code

### Активация агентов

**1. Явный вызов (через Task tool):**

```typescript
Task({
  subagent_type: "systematic-plan-reviewer",
  description: "Validate plan structure",
  prompt: "Analyze plan structure in docs/PLAN/feature-auth.md for violations..."
})
```

**2. Рекомендация от другого агента:**

После завершения work-plan-architect:
```
✅ work-plan-architect completed

🔄 Recommended Next Actions:
1. 🚨 CRITICAL: work-plan-reviewer
   Command: Use Task tool with subagent_type: "work-plan-reviewer"
```

Claude Code интерпретирует это как сигнал к вызову следующего агента.

### Передача контекста между агентами

**Явная передача через параметры:**
```typescript
Task({
  subagent_type: "work-plan-reviewer",
  prompt: `Review plan at docs/PLAN/feature-auth.md
           Context from architect: 8 tasks, 3 new components
           Focus: Validate against common-plan-generator.mdc`
})
```

**Неявная передача через файлы:**
- Агент А создаёт файл `docs/PLAN/feature-auth.md`
- Агент А рекомендует агента Б с параметром `plan_file`
- Агент Б читает файл и продолжает работу

## 🛡️ Защита от бесконечных циклов

### Глобальные лимиты итераций

Система агентов имеет встроенную защиту от бесконечных циклов через явные лимиты итераций и механизмы эскалации:

| Цикл | Агенты | Макс итераций | Эскалация |
|------|--------|---------------|-----------|
| **Quality Cycle** | architect ↔ reviewer | 3 | Детальный отчёт нерешённых проблем |
| **Systematic Review** | systematic ↔ architect | 2 | Violations report к пользователю |
| **Test Healing** | test-healer self-loop | 2 | Диагностика + требование arch review |
| **Execution Review** | executor modes | 2 | Через pre-completion-validator |
| **Parallel Execution** | code reviewers | N/A | Только параллельный запуск |

### Механизм эскалации

При достижении лимита итераций активируется трёхступенчатая эскалация:

**1. Формирование детального отчёта:**
```markdown
⚠️ CYCLE LIMIT REACHED - ESCALATION TO USER ⚠️

Cycle: [architect ↔ reviewer]
Iterations completed: 3/3 (limit reached)
Duration: [time elapsed]

UNRESOLVED ISSUES:
- Issue 1: [description]
  Attempted fixes: [what was tried]
  Why failed: [root cause]

- Issue 2: [description]
  Attempted fixes: [what was tried]
  Why failed: [root cause]

RECOMMENDED ACTIONS:
- Manual intervention required for architectural decisions
- Consider alternative approach: [suggestions]
- Consult with [relevant expert/team]
```

**2. Передача управления пользователю:**
- ❌ **НЕ продолжать** автономную работу
- ✅ **Предоставить** actionable рекомендации
- ✅ **Указать** возможные причины блокировки
- ✅ **Предложить** альтернативные подходы

**3. Документирование блокировки:**
- Сохранить cycle history для анализа
- Зафиксировать неустранимые проблемы
- Создать issue/task для manual resolution

### Cycle Detection & Tracking

Каждый агент при вызове следующего агента передаёт метаданные для отслеживания циклов:

```json
{
  "cycle_tracking": {
    "cycle_id": "architect-reviewer-2025-10-09-1234",
    "iteration_count": 2,
    "max_iterations": 3,
    "agents_path": ["work-plan-architect", "work-plan-reviewer", "work-plan-architect"],
    "started_at": "2025-10-09T10:30:00Z",
    "issues_history": [
      {
        "iteration": 1,
        "issues_found": 5,
        "issues_fixed": 3,
        "remaining": 2
      },
      {
        "iteration": 2,
        "issues_found": 2,
        "issues_fixed": 1,
        "remaining": 1
      }
    ]
  }
}
```

### Параллельное выполнение (code reviewers)

**code-principles-reviewer** и **code-style-reviewer** используют специальный pattern:

```markdown
**⚠️ EXECUTION MODE**: PARALLEL - use single message with multiple Task calls
**❌ ANTI-PATTERN**: Sequential execution (principles → style → principles) creates cycle
```

**Правильная реализация:**
```typescript
// ✅ CORRECT: Parallel execution (no cycle)
[
  Task({ subagent_type: "code-principles-reviewer", ... }),
  Task({ subagent_type: "code-style-reviewer", ... })
]
```

**Неправильная реализация:**
```typescript
// ❌ WRONG: Sequential execution (creates cycle)
Task({ subagent_type: "code-principles-reviewer", ... })
// wait for completion, then:
Task({ subagent_type: "code-style-reviewer", ... })
```

### Визуализация прогресса цикла

В рекомендациях агенты показывают прогресс итераций:

```markdown
🔄 Cycle Tracking: Iteration 2/3 (architect → reviewer → architect)
⚠️ Approaching iteration limit - escalation at 3 iterations

Issues Progress:
- Iteration 1: 5 issues found → 3 fixed (60% resolution)
- Iteration 2: 2 issues remaining → 1 fixed (50% resolution)
- **Remaining**: 1 unresolved issue (requires user decision)
```

### Мониторинг здоровья циклов

**Success Metrics:**
- ✅ <90% задач разрешаются за 1-2 итерации
- ✅ 5-10% требуют 3 итерации
- ⚠️ <5% достигают лимита → эскалация

**Warning Triggers:**
- 🟡 Iteration 2 без прогресса (same issues remaining)
- 🟠 Iteration 3 достигнута (approaching limit)
- 🔴 Limit reached (immediate escalation)

### Ссылки на детальный анализ

Полный анализ циклов и защиты: [../docs/AGENTS_CYCLE_PROTECTION_ANALYSIS.md](../docs/AGENTS_CYCLE_PROTECTION_ANALYSIS.md)

**Содержит:**
- Граф всех возможных переходов агентов
- Выявленные потенциальные циклы (6 типов)
- Детальные рекомендации по защите
- Таблица текущего состояния защиты (60% частично защищены, 20% не защищены, 20% полностью защищены → цель: 100%)

## 📈 Ожидаемые метрики

### После внедрения P0 агентов (Фаза 1):

| Метрика | До | После P0 | Улучшение |
|---------|-----|----------|-----------|
| **Systematic review time** | 30-60 мин | 1-2 мин | **20-30x** |
| **Plan validation coverage** | 60-70% | 95%+ | **+30%** |
| **Review consolidation** | Ручная | Автоматическая | **100%** |
| **Пропуск этапов валидации** | 20-30% | <5% | **4-6x** |

### После внедрения всех агентов (Фаза 3):

| Метрика | До | После | Улучшение |
|---------|-----|-------|-----------|
| **Full review cycle time** | 1.5-2.5 часа | 10-15 мин | **10-15x** |
| **Автоматизация workflow** | 50% | 90% | **+40%** |
| **LLM Readiness score** | Не измеряется | ≥90% | **✅ Новая метрика** |
| **Documentation drift** | 20-30% устаревшего | <5% | **4-6x** |
| **Performance regressions** | Не отслеживаются | Автоматический мониторинг | **✅ Новая метрика** |

## 🔐 Безопасность и ограничения

### Git Workflow Safety

**Критические ограничения:**
- ❌ НИКОГДА не коммитить без подтверждения пользователя
- ❌ НИКОГДА не делать force push
- ❌ НИКОГДА не коммитить в main/master без PR
- ✅ Всегда проверять authorship перед amend
- ✅ Всегда запускать pre-commit hooks

**Реализация:** git-workflow-manager агент (P1).

### Execution Limits

**Timeouts:**
- Lightweight агенты (validators): 5-10 минут
- Medium агенты (reviewers): 20-30 минут
- Heavy агенты (executors): 120-480 минут

**Iteration Limits:**
- Максимум 3 итерации для циклических этапов (architect↔reviewer)
- После 3 неудач → эскалация пользователю

**Resource Limits:**
- Максимум 5 параллельных агентов (review-consolidator)
- Автоматическая очередь при превышении

## 📚 Связанные документы

**Анализ и исследование:**
- [../docs/AGENTS_ECOSYSTEM_ANALYSIS.md](../docs/AGENTS_ECOSYSTEM_ANALYSIS.md) - комплексный анализ существующей экосистемы

**Спецификации агентов:**
- [agents/](agents/) - директория со спецификациями всех агентов
- [agents/systematic-plan-reviewer.md](agents/systematic-plan-reviewer.md) - пример P0 агента

**Шаблоны:**
- [templates/agent-template.md](templates/agent-template.md) - универсальный шаблон агента

**Интеграция:**
- [../CLAUDE.md](../CLAUDE.md) - краткая справка по агентам для пользователя
- [../docs/PLANS/](../docs/PLANS/) - планы работ для исполнения

---

**Версия:** 1.0
**Дата создания:** 2025-10-08
**Авторы:** Claude Code
**Статус:** ✅ Активная архитектура
