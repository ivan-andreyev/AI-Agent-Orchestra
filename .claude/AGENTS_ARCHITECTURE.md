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
| **plan-readiness-validator** | work-plan-architect (if <90% score, max 3)<br>architecture-documenter (if arch components) | plan-task-executor (if ≥90%)<br>parallel-plan-optimizer (if >5 tasks) |
| **plan-task-executor** | plan-review-iterator (always after execution)<br>review-consolidator (if code written) | - |
| **plan-review-iterator** | plan-task-completer (if reviews satisfied 80%+) | Self-iteration (if issues found, max 2) |
| **plan-task-completer** | work-plan-reviewer (always)<br>plan-task-executor (next task) | review-consolidator (before completion)<br>parallel-plan-optimizer (if ≥3 ready tasks)<br>architecture-documenter (if arch change) |
| **review-consolidator** | plan-task-executor (if P0 issues)<br>pre-completion-validator (if no P0) | git-workflow-manager (if ready to commit) |
| **test-healer** | pre-completion-validator (if 100%) | code-principles-reviewer (if DI issues) |
| **code-principles-reviewer** | code-style-reviewer (parallel) | architecture-documenter (if violations) |
| **code-style-reviewer** | code-principles-reviewer (parallel) | - |
| **architecture-documenter** | - | work-plan-architect (if redesign needed) |
| **parallel-plan-optimizer** | plan-task-executor | architecture-documenter |
| **pre-completion-validator** | work-plan-reviewer (if mismatches)<br>git-workflow-manager (if OK) | - |
| **git-workflow-manager** | pre-completion-validator (before commit) | test-healer (if tests not run) |

## 📈 Метрики системы агентов

### Review System Metrics (review-consolidator)
- **Parallel Review Performance**: 60-70% time savings vs sequential execution
- **Deduplication Efficiency**: 70-80% issue reduction (127 raw → 35 consolidated)
- **Cycle Protection**: Max 2 review-fix cycles, automatic escalation on cycle 3
- **Performance Target**: <6 minutes for 100 files with 3 reviewers
- **Memory Efficiency**: <500MB peak usage during consolidation

### Plan Validation Metrics (plan-readiness-validator)
- **LLM Readiness Score**: 0-100 scale with ≥90% threshold
- **Iteration Success Rate**: 85-90% plans pass after 1-2 iterations
- **Escalation Rate**: <10% require manual intervention after 3 cycles
- **Validation Time**: <2 minutes per plan (100+ tasks)

### Plan Review Metrics (systematic-plan-reviewer)
- **Automation Level**: 95% of systematic review automated via PowerShell
- **Time Savings**: 30-60 minutes manual → 1-2 minutes automated
- **False Positive Rate**: <5% (high precision)
- **Coverage**: 100% of plan structure rules validated

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
1. ✅ **systematic-plan-reviewer** (COMPLETE)
   - Автоматизирует systematic review через PowerShell скрипты
   - Заменяет 30-60 минут ручной работы на 1-2 минуты

2. ✅ **plan-readiness-validator** (COMPLETE)
   - Оценивает LLM готовность планов (≥90% score)
   - Предотвращает провальные попытки исполнения

3. ✅ **review-consolidator** (COMPLETE)
   - Координирует параллельный запуск армии ревьюеров
   - Консолидирует результаты в единый отчёт
   - **Performance**: <6 min для 100 файлов, 60-70% parallel speedup, 70-80% deduplication

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

---

## 🔍 Детальные переходы агентов

### plan-readiness-validator Transitions

**Агент**: plan-readiness-validator (P0, Production v1.0.0)
**Тип**: Validation Agent (Pre-Execution)
**Функция**: Validates work plan LLM readiness with ≥90% threshold scoring

**Спецификация**: `.cursor/agents/plan-readiness-validator/agent.md`
**Документация**: `.cursor/agents/plan-readiness-validator/README.md`

#### CRITICAL Transitions (Обязательные пути)

##### 1. work-plan-architect → plan-readiness-validator (Post-Planning Validation)

**Условие**: After plan creation or revision, before finalization
**Приоритет**: CRITICAL (quality gate for execution)
**Макс итераций**: 3 (validation → revision → validation cycle)
**Цикл защиты**: Mandatory escalation after 3 iterations without READY status

**Параметры**:
```typescript
{
  plan_file: string,                    // Path to work plan file/directory
  validation_type?: "post_planning",    // Validation context
  cycle_tracking?: {
    cycle_id: string,                   // Unique cycle identifier
    iteration_count: number,            // Current iteration (1-3)
    max_iterations: 3,                  // Maximum allowed iterations
    previous_score?: number             // Score from previous iteration
  }
}
```

**Task Tool Invocation Example**:
```typescript
// Iteration 1: Initial validation
Task({
  subagent_type: "plan-readiness-validator",
  description: "Validate newly created work plan for LLM execution readiness",
  prompt: `
    Validate plan at: Docs/plans/Feature-Authentication-Plan.md

    Validation Requirements:
    - Task Specificity: Concrete file paths, class names, acceptance criteria
    - Technical Completeness: Entity/Service/API integration steps
    - Execution Clarity: Tasks ≤30 tool calls, dependencies clear
    - Structure Compliance: GOLDEN RULES, file size ≤400 lines

    Expected Output:
    - LLM Readiness Score (0-100)
    - Status: READY (≥90%) or REQUIRES_IMPROVEMENT (<90%)
    - Detailed issues list if REQUIRES_IMPROVEMENT

    Cycle Tracking:
    - Cycle ID: validator-architect-feature-auth
    - Iteration: 1/3
  `
})

// Expected Output (REQUIRES_IMPROVEMENT):
/*
Plan Readiness Validation Report

Plan: Feature-Authentication-Plan.md
Status: REQUIRES_IMPROVEMENT
LLM Readiness Score: 76/100

Critical Issues:
- Task 2.1 missing DbContext integration (Technical Completeness -10)
- Task 3.1 complexity exceeds 30 tool calls (Execution Clarity -5)
- 5 tasks lack specific file paths (Task Specificity -9)

Recommended Next Actions:
1. CRITICAL: work-plan-architect (revision required)
   Command: Use Task tool with subagent_type: "work-plan-architect"
   Parameters:
     revision_mode: true
     plan_file: "Docs/plans/Feature-Authentication-Plan.md"
     issues_list: [
       "Task 2.1 missing DbContext integration",
       "Task 3.1 complexity exceeds 30 tool calls",
       "Tasks 2.2, 2.3, 3.2, 3.3, 4.1 missing specific file paths"
     ]
   Cycle Tracking:
     cycle_id: "validator-architect-feature-auth"
     iteration_count: 2
     max_iterations: 3
*/

// Iteration 2: After architect revision
Task({
  subagent_type: "plan-readiness-validator",
  description: "Re-validate revised work plan (iteration 2/3)",
  prompt: `
    Re-validate plan at: Docs/plans/Feature-Authentication-Plan.md

    Context:
    - Iteration: 2/3 (approaching limit)
    - Previous Score: 76/100
    - Architect addressed: DbContext integration, file paths
    - Remaining concern: Task 3.1 complexity

    Focus Areas:
    - Verify DbContext integration steps added
    - Verify file paths now present
    - Check if Task 3.1 decomposed

    Cycle Tracking:
    - Cycle ID: validator-architect-feature-auth
    - Iteration: 2/3
    - Previous Score: 76/100
  `
})

// Expected Output (Improved but still REQUIRES_IMPROVEMENT):
/*
Plan Readiness Validation Report

Plan: Feature-Authentication-Plan.md
Status: REQUIRES_IMPROVEMENT
LLM Readiness Score: 87/100 (+11 points)

Score Breakdown:
- Task Specificity: 28/30 (fixed: +9)
- Technical Completeness: 30/30 (fixed: +10)
- Execution Clarity: 11/20 (unfixed: Task 3.1 still 35 tool calls)
- Structure Compliance: 18/20 (unchanged)

Progress Tracking:
- Iteration 1 → 2: +11 points (good progress)
- Issues Resolved: 2/3 (67%)
- Issues Remaining: 1 (Task 3.1 complexity)

WARNING: Iteration 2/3 - approaching cycle limit

Recommended Next Actions:
1. CRITICAL: work-plan-architect (final revision attempt)
   Command: Use Task tool with subagent_type: "work-plan-architect"
   Parameters:
     revision_mode: true
     plan_file: "Docs/plans/Feature-Authentication-Plan.md"
     issues_list: ["Task 3.1 still exceeds 30 tool calls (estimated 35), needs decomposition into 3.1A, 3.1B, 3.1C"]
   Cycle Tracking:
     cycle_id: "validator-architect-feature-auth"
     iteration_count: 3
     max_iterations: 3
     WARNING: Final iteration before escalation
*/

// Iteration 3: Final validation attempt
Task({
  subagent_type: "plan-readiness-validator",
  description: "Final validation (iteration 3/3) - escalation if fails",
  prompt: `
    Final validation at: Docs/plans/Feature-Authentication-Plan.md

    Context:
    - Iteration: 3/3 (FINAL ATTEMPT)
    - Previous Scores: 76 → 87
    - Score Trend: Improving (+11 points last iteration)
    - Last Issue: Task 3.1 complexity (35 tool calls)

    CRITICAL: If score still <90%, escalate to user with detailed report

    Cycle Tracking:
    - Cycle ID: validator-architect-feature-auth
    - Iteration: 3/3 (LIMIT REACHED)
    - Previous Scores: 76 → 87
  `
})

// Expected Output (Success - READY):
/*
Plan Readiness Validation Report

Plan: Feature-Authentication-Plan.md
Status: READY
LLM Readiness Score: 92/100 (+5 points)

Score Breakdown:
- Task Specificity: 28/30
- Technical Completeness: 30/30
- Execution Clarity: 18/20 (fixed: Task 3.1 decomposed into 3.1A, 3.1B, 3.1C)
- Structure Compliance: 16/20

Progress Tracking:
- Iteration 1 → 2: +11 points
- Iteration 2 → 3: +5 points
- Total Improvement: +16 points
- Issues Resolved: 3/3 (100%)

SUCCESS: Cycle completed within 3 iterations

Recommended Next Actions:
1. RECOMMENDED: plan-task-executor (begin execution)
   Command: Use Task tool with subagent_type: "plan-task-executor"
   Parameters:
     plan_file: "Docs/plans/Feature-Authentication-Plan.md"

2. CRITICAL: architecture-documenter (5 architectural components detected)
   Command: Use Task tool with subagent_type: "architecture-documenter"
   Parameters:
     type: "planned"
     plan_file: "Docs/plans/Feature-Authentication-Plan.md"
     components: ["AuthService", "TokenValidator", "UserRepository", "AuthController", "RefreshTokenEntity"]
*/

// Escalation Scenario (if iteration 3 fails):
/*
ESCALATION REQUIRED - CYCLE LIMIT REACHED

Cycle: validator ↔ architect
Iterations Completed: 3/3 (limit reached)
Final Score: 88/100 (still below 90% threshold)

Score Progression:
- Iteration 1: 76/100 (REQUIRES_IMPROVEMENT)
- Iteration 2: 87/100 (REQUIRES_IMPROVEMENT, +11)
- Iteration 3: 88/100 (REQUIRES_IMPROVEMENT, +1)

UNRESOLVED ISSUES:
1. Structure Compliance: 14/20 (-6 points)
   - Issue: File "phase-3-api-implementation.md" exceeds 400 lines (actual: 487 lines)
   - Attempted Fix: Architect condensed content
   - Why Failed: Content inherently complex, needs 4 child files instead of 3
   - Root Cause: Underestimation of API layer complexity

RECOMMENDED MANUAL INTERVENTION:
- Manually review API implementation phase for decomposition opportunities
- Consider splitting phase-3 into phase-3A (Controllers), phase-3B (Middleware), phase-3C (Auth), phase-3D (Validation)
- Alternative: Accept 88/100 score with manual oversight during execution
- Consult with architect: Is API phase truly atomic or can it be further decomposed?

ALTERNATIVE APPROACHES:
- Simplify feature scope: Remove refresh token functionality (reduces complexity by ~15%)
- Defer complex validations to Phase 2 (implement basic auth first, add advanced features later)
- Accept controlled technical debt: Document 88/100 acceptance with manual review commitment
*/
```

**Цикл защиты**:
```markdown
Iteration Tracking Format: "Iteration 2/3: 76→87 (+11 pts)"

Max Iterations: 3
- Iteration 1: Initial validation, identify issues
- Iteration 2: Re-validate after first revision
- Iteration 3: Final validation attempt

Escalation Trigger: iteration_count >= 3 AND score < 90

Escalation Report Format:
- Final Score: X/100 (target: ≥90)
- Score Progression: [iteration_1 → iteration_2 → iteration_3]
- Unresolved Issues: [list with root cause analysis]
- Recommended Manual Intervention: [specific actions]
- Alternative Approaches: [scope reduction, phased implementation]
```

##### 2. plan-readiness-validator → work-plan-architect (Revision Loop)

**Условие**: LLM readiness score <90% (REQUIRES_IMPROVEMENT status)
**Приоритет**: CRITICAL (revision mandatory before execution)
**Макс итераций**: 3 total (counting from initial validation)
**Цикл защиты**: Track iteration count, escalate at limit

**Параметры**:
```typescript
{
  revision_mode: true,
  plan_file: string,
  issues_list: string[],              // Specific issues from validation report
  cycle_tracking: {
    cycle_id: string,
    iteration_count: number,          // Increment on each revision
    max_iterations: 3,
    previous_score: number,           // For progress tracking
    score_trend?: string              // "improving", "stagnant", "declining"
  }
}
```

**Документация**: См. примеры в Transition #1 выше (iterations 1-3)

##### 3. plan-readiness-validator → architecture-documenter (Architectural Components)

**Условие**: Plan contains architectural components (Entity, Service, Controller, Interface patterns detected)
**Приоритет**: CRITICAL (architectural documentation mandatory before execution)
**Макс итераций**: 1 (documentation generation, no iteration)

**Параметры**:
```typescript
{
  type: "planned",                     // Planned architecture (not yet implemented)
  plan_file: string,
  components: string[],                // Detected architectural components
  location?: string                    // Output location (default: Docs/Architecture/Planned/)
}
```

**Task Tool Invocation Example**:
```typescript
Task({
  subagent_type: "architecture-documenter",
  description: "Document planned architecture from validated work plan",
  prompt: `
    Create planned architecture documentation for plan:
    Docs/plans/Feature-Authentication-Plan.md

    Detected Architectural Components:
    - AuthService (service layer)
    - TokenValidator (validation logic)
    - UserRepository (data access layer)
    - AuthController (API controller)
    - RefreshTokenEntity (database entity)

    Documentation Requirements:
    - Component relationships and dependencies
    - Interface definitions (IAuthenticationService, ITokenService, IUserRepository)
    - Data flow diagrams (login flow, token refresh flow)
    - Integration points (DbContext, DI container, middleware pipeline)

    Output Location: Docs/Architecture/Planned/feature-authentication-architecture.md

    Type: planned (architecture not yet implemented)
  `
})

// Expected Output:
/*
Architecture documentation created:
- File: Docs/Architecture/Planned/feature-authentication-architecture.md
- Components Documented: 5
- Diagrams: 2 (login flow, token refresh flow)
- Integration Points: 3 (DbContext, DI, Middleware)

Next Steps:
- Review architectural documentation for correctness
- Proceed to plan-task-executor with validated plan
*/
```

#### RECOMMENDED Transitions (Рекомендуемые пути)

##### 4. plan-readiness-validator → plan-task-executor (Execution Start)

**Условие**: LLM readiness score ≥90% (READY status)
**Приоритет**: RECOMMENDED (execution is next logical step)
**Макс итераций**: N/A (execution begins, no validation iteration)

**Параметры**:
```typescript
{
  plan_file: string,                   // Path to validated READY plan
  execution_mode?: "sequential",       // Default execution mode
  validation_score?: number            // For reference (passed from validator)
}
```

**Task Tool Invocation Example**:
```typescript
// After successful validation (score ≥90%)
Task({
  subagent_type: "plan-task-executor",
  description: "Begin execution of validated work plan",
  prompt: `
    Execute plan at: Docs/plans/Feature-Authentication-Plan.md

    Validation Context:
    - LLM Readiness Score: 93/100 (READY)
    - Validation Date: 2025-10-16
    - All integration steps verified
    - Execution complexity within limits

    Execution Instructions:
    - Follow BOTTOM-UP principle (deepest task first)
    - Execute ONE task at a time
    - STOP after each task completion
    - Recommend plan-review-iterator after execution

    Plan Summary:
    - Total Tasks: 12
    - Estimated Duration: 15-20 hours
    - Architectural Components: 5 (documented in Architecture/Planned/)
  `
})

// Expected Output:
/*
plan-task-executor started execution

Next Task Identified:
- Task 1.1: Setup Project Structure
- Dependencies: None (first task)
- Estimated Complexity: 8 tool calls
- Status: Ready for execution

Execution will begin immediately...
*/
```

##### 5. plan-readiness-validator → parallel-plan-optimizer (Parallelization Opportunity)

**Условие**: Plan has >5 tasks (parallelization could reduce execution time)
**Приоритет**: RECOMMENDED (optional optimization, 40-50% time reduction)
**Макс итераций**: 1 (analysis only, no iteration)

**Параметры**:
```typescript
{
  plan_file: string,
  task_count: number,                  // Detected task count
  validation_score?: number            // For reference
}
```

**Task Tool Invocation Example**:
```typescript
// After READY status with >5 tasks
Task({
  subagent_type: "parallel-plan-optimizer",
  description: "Analyze plan for parallel execution opportunities",
  prompt: `
    Analyze plan for parallelization:
    Docs/plans/Feature-Authentication-Plan.md

    Context from Validation:
    - Total Tasks: 12
    - LLM Readiness Score: 93/100 (READY)
    - Validation Date: 2025-10-16

    Analysis Requirements:
    - Identify independent tasks (no shared dependencies)
    - Group tasks by dependency levels
    - Estimate time reduction with parallel execution
    - Generate parallel execution strategy

    Expected Outcome:
    - Parallel execution graph
    - Time reduction estimate (% improvement)
    - Risk assessment (complexity increase vs. time savings)
  `
})

// Expected Output:
/*
Parallel Execution Analysis Complete

Parallelization Opportunities:
- Level 1 (no dependencies): Tasks 1.1, 1.2, 1.3 (3 tasks, parallel)
- Level 2 (depends on Level 1): Tasks 2.1, 2.2 (2 tasks, parallel)
- Level 3 (depends on Level 2): Tasks 3.1, 3.2, 3.3 (3 tasks, parallel)
- Level 4 (depends on Level 3): Tasks 4.1, 4.2, 4.3, 4.4 (4 tasks, parallel)

Time Reduction Estimate:
- Sequential Execution: 20 hours
- Parallel Execution: 12 hours
- Time Savings: 40% (8 hours saved)

Recommended Approach:
- Use parallel execution for Levels 1, 2, 3, 4
- Execute levels sequentially (wait for level completion before starting next)
- Monitor resource usage (max 3 parallel tasks at once)

Next Steps:
- Proceed with parallel-plan-optimizer generated strategy
- Invoke plan-task-executor with parallel execution mode
*/
```

#### OPTIONAL Transitions (Дополнительные пути)

##### 6. systematic-plan-reviewer → plan-readiness-validator (Complementary Validation)

**Условие**: After manual structural review (optional complementary validation)
**Приоритет**: OPTIONAL (systematic-plan-reviewer focuses on structure, plan-readiness-validator on LLM readiness)
**Макс итераций**: 1

**Параметры**:
```typescript
{
  plan_file: string,
  systematic_review_result?: string,   // Result from systematic-plan-reviewer (if available)
  validation_focus?: "execution_readiness" // Focus area for validator
}
```

**Task Tool Invocation Example**:
```typescript
// After systematic-plan-reviewer completes
Task({
  subagent_type: "plan-readiness-validator",
  description: "Validate LLM execution readiness after structural review",
  prompt: `
    Validate plan at: Docs/plans/Feature-Authentication-Plan.md

    Context:
    - Systematic review completed (PASSED)
    - Structure compliance: 100%
    - GOLDEN RULES validated

    Validation Focus:
    - LLM execution readiness (complementary to structural review)
    - Task specificity and technical completeness
    - Execution complexity estimation

    Expected Outcome:
    - LLM Readiness Score with focus on execution dimensions
    - Combined validation report (structure + execution readiness)
  `
})

// Expected Output:
/*
Plan Readiness Validation Report

Plan: Feature-Authentication-Plan.md
Status: READY
LLM Readiness Score: 94/100

Combined Validation Results:
- Structure Compliance: 20/20 (verified by systematic-plan-reviewer)
- Task Specificity: 28/30
- Technical Completeness: 30/30
- Execution Clarity: 16/20

Complementary Validation Summary:
- Systematic Review: PASSED (structure focus)
- Readiness Validation: READY (execution focus)
- Overall Confidence: HIGH (both validations passed)

Recommended Next Actions:
1. RECOMMENDED: plan-task-executor (dual validation passed)
2. CRITICAL: architecture-documenter (5 components detected)
*/
```

---

### Cycle Protection Summary для plan-readiness-validator

**Protected Cycles**:
- **validator ↔ architect**: Max 3 iterations, escalation after limit

**Iteration Tracking**:
```json
{
  "cycle_id": "validator-architect-{plan-name}",
  "iteration_count": 2,
  "max_iterations": 3,
  "score_history": [76, 87],
  "score_trend": "improving",
  "issues_resolved": 2,
  "issues_remaining": 1
}
```

**Escalation Triggers**:
- Iteration 2/3: WARNING issued ("approaching cycle limit")
- Iteration 3/3: FINAL ATTEMPT marked
- Score <90% after iteration 3: IMMEDIATE ESCALATION

**Escalation Report Format**:
```markdown
ESCALATION REQUIRED - CYCLE LIMIT REACHED

Cycle: validator ↔ architect
Iterations: 3/3 (limit reached)
Final Score: {score}/100 (target: ≥90)

Score Progression:
- Iteration 1: {score_1}/100
- Iteration 2: {score_2}/100 ({delta} points)
- Iteration 3: {score_3}/100 ({delta} points)

UNRESOLVED ISSUES:
{detailed_issue_list_with_root_cause}

RECOMMENDED MANUAL INTERVENTION:
{specific_actionable_recommendations}

ALTERNATIVE APPROACHES:
{scope_reduction_or_phased_implementation_options}
```

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
