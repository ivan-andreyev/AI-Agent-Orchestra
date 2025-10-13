---
name: agent-name-in-kebab-case
description: "Краткое описание агента (1-2 предложения) - что он делает, какую проблему решает и основные возможности. Используй конкретные глаголы и избегай общих фраз."
tools: Bash, Read, Write, Edit, Glob, Grep, TodoWrite
model: sonnet
color: blue
---

# [Agent Name] Agent

## 📖 AGENTS ARCHITECTURE REFERENCE

**READ `.claude/AGENTS_ARCHITECTURE.md` WHEN:**
- ⚠️ **[Uncertain situation 1]** (example: unclear escalation format, reaching max iterations)
- ⚠️ **[Uncertain situation 2]** (example: coordinating with other agents, non-standard scenarios)
- ⚠️ **[Uncertain situation 3]** (example: complex edge cases, workflow conflicts)

**FOCUS ON SECTIONS:**
- **"📊 Матрица переходов агентов"** - [what to find: agent coordination patterns, transitions]
- **"🛡️ Защита от бесконечных циклов"** - [what to find: iteration limits, escalation procedures]
- **"🏛️ Архитектурные принципы"** - [what to find: core patterns, design principles]

**DO NOT READ** for [standard cases description - when agent knows what to do without architecture doc].

## 🎯 НАЗНАЧЕНИЕ

**[Одно предложение - что делает агент]**

**Проблема, которую решает:**
- ❌ Проблема 1 (конкретная, с примером)
- ❌ Проблема 2 (конкретная, с примером)
- ❌ Проблема 3 (конкретная, с примером)

**Решение:**
- ✅ Решение 1 (как именно решаем проблему 1)
- ✅ Решение 2 (как именно решаем проблему 2)
- ✅ Решение 3 (как именно решаем проблему 3)

## 🛠️ ИНСТРУМЕНТЫ

### Tools используемые агентом:

1. **Tool Name** - основное назначение
   - Специфика использования для этого агента
   - Примеры применения
   - Ограничения если есть

2. **Another Tool** - основное назначение
   - Специфика использования для этого агента
   - Примеры применения
   - Ограничения если есть

## 📋 WORKFLOW

### Этап 1: [НАЗВАНИЕ ЭТАПА ЗАГЛАВНЫМИ]

**Цель:** [Что достигаем на этом этапе одним предложением]

**Шаги:**

1. **[Действие 1]:**
   ```
   Детали действия:
   - Подшаг 1
   - Подшаг 2
   - Ожидаемый результат
   ```

2. **[Действие 2]:**
   ```
   Детали действия:
   - Подшаг 1
   - Подшаг 2
   - Ожидаемый результат
   ```

3. **[Критерии успеха/проверки]:**
   - ✅ Критерий 1
   - ✅ Критерий 2
   - ✅ Критерий 3

**Output:** [Что получаем в результате этого этапа]

### Этап 2: [НАЗВАНИЕ ЭТАПА ЗАГЛАВНЫМИ]

**Цель:** [Что достигаем на этом этапе]

**Шаги:**

1. **[Действие 1]:**
   - Детали
   - Примеры
   - Проверки

2. **[Действие 2]:**
   - Детали
   - Примеры
   - Проверки

**Output:** [Что получаем в результате]

### Этап 3: [ФИНАЛЬНЫЙ ЭТАП]

**Цель:** [Финализация работы агента]

**Шаги:**

1. **Генерация результата:**
   - Формат вывода
   - Обязательные элементы
   - Опциональные элементы

2. **Сохранение (если применимо):**
   - Путь к файлу
   - Формат файла
   - Naming convention

**Output:** [Финальный результат работы агента]

---

## 🔄 АВТОМАТИЧЕСКИЕ РЕКОМЕНДАЦИИ

### При успешном завершении:

**CRITICAL:**
- **agent-name**: [Когда ОБЯЗАТЕЛЬНО вызывать этот агент]
  - Condition: [Точное условие активации]
  - Reason: [Почему это критично - с конкретикой]
  - Command: Use Task tool with subagent_type: "agent-name"
  - Parameters:
    ```
    param1: [значение/описание]
    param2: [значение/описание]
    ```

**RECOMMENDED:**
- **agent-name**: [Когда рекомендуется вызывать]
  - Condition: [Условие активации]
  - Reason: [Почему это улучшит результат]
  - Command: Use Task tool with subagent_type: "agent-name"

### При обнаружении проблем:

**CRITICAL:**
- **agent-name**: [Агент для исправления проблем]
  - Condition: [Какие проблемы обнаружены]
  - Reason: [Что нужно исправить]
  - Parameters:
    ```
    issues: [список проблем]
    context: [контекст]
    ```
  - **⚠️ MAX_ITERATIONS**: [число если применимо]
  - **⚠️ ESCALATION**: [Условия эскалации к пользователю]

**User Escalation:**
- Condition: [Когда эскалировать к пользователю]
- Format:
  ```markdown
  ⚠️ [ТАЙТЛ ЭСКАЛАЦИИ] ⚠️

  Agent: [agent-name]
  Issue: [описание проблемы]

  UNRESOLVED PROBLEMS:
  - Problem 1: [описание]
    Attempted: [что пытались]
    Why failed: [почему не получилось]

  RECOMMENDED ACTIONS:
  - [конкретное действие для пользователя]
  - [альтернативный подход]
  ```

### Conditional recommendations:

- **IF** [условие] **THEN** recommend **agent-name**
  - Reason: [Почему в этом случае]
  - Example: IF план >5 задач THEN recommend parallel-plan-optimizer

- **IF** [другое условие] **THEN** recommend **другой-агент**
  - Reason: [Почему в этом случае]

### Example output:

```
✅ [agent-name] completed: [краткое описание результата]

[Summary Section:]
- Metric 1: [значение]
- Metric 2: [значение]
- Metric 3: [значение]

[Detailed Results:]
- Result 1: [описание]
- Result 2: [описание]
- Result 3: [описание]

Duration: [время выполнения]

🔄 Recommended Next Actions:

1. 🚨 CRITICAL: agent-name
   Reason: [причина почему критично]
   Command: Use Task tool with subagent_type: "agent-name"
   Parameters:
     param1: [значение]
     param2: [значение]

2. ⚠️ RECOMMENDED: agent-name
   Reason: [причина почему рекомендуется]
   Command: Use Task tool with subagent_type: "agent-name"

3. 💡 OPTIONAL: agent-name
   Reason: [причина почему опционально]
   Condition: [когда применимо]
   Command: Use Task tool with subagent_type: "agent-name"
```

---

## 📊 МЕТРИКИ УСПЕХА

### ОБЯЗАТЕЛЬНЫЕ РЕЗУЛЬТАТЫ:
1. **Result 1** (описание что должно быть достигнуто)
2. **Result 2** (описание что должно быть достигнуто)
3. **Result 3** (описание что должно быть достигнуто)

### ПОКАЗАТЕЛИ КАЧЕСТВА:
- **Quality metric 1**: [target value] ([описание как измеряем])
- **Quality metric 2**: [target value] ([описание как измеряем])
- **Quality metric 3**: [target value] ([описание как измеряем])

### Производительность:
- **Performance metric 1**: [baseline] → [target] ([improvement])
- **Performance metric 2**: [baseline] → [target] ([improvement])
- **Time per operation**: [time estimate]

### Качество:
- **Quality metric 1**: [baseline] → [target] ([improvement])
- **Quality metric 2**: [baseline] → [target] ([improvement])
- **Accuracy**: [percentage target]

---

## 🔗 ИНТЕГРАЦИЯ

### С существующими агентами:

**agent-name:**
- Когда вызывается: [условия вызова]
- Что получает: [входные данные]
- Что передаёт: [выходные данные]
- Тип связи: [CRITICAL/RECOMMENDED/OPTIONAL]

**another-agent:**
- Когда вызывается: [условия вызова]
- Что получает: [входные данные]
- Что передаёт: [выходные данные]
- Тип связи: [CRITICAL/RECOMMENDED/OPTIONAL]

### С правилами:

Применяет правила из:
- **`@rule-name.mdc`** - [для чего используется]
  - [Конкретная секция или принцип]
  - [Конкретная секция или принцип]

- `@another-rule.mdc` - [для чего используется]

---

## 🧪 ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ

### Пример 1: [Название сценария]

**Input:**
```markdown
User: [Запрос пользователя]
Context: [Дополнительный контекст если нужен]
```

**Process:**
```
1. [Этап 1]:
   - Действие 1
   - Действие 2
   - Результат

2. [Этап 2]:
   - Действие 1
   - Действие 2
   - Результат

3. [Этап 3]:
   - Действие 1
   - Результат
```

**Output:**
```
✅ [Результат работы]
→ [Рекомендации]
```

### Пример 2: [Другой сценарий]

**Input:**
```markdown
User: [Запрос пользователя]
Context: [Контекст]
```

**Process:**
```
[Шаги обработки]
```

**Output:**
```
✅ [Результат]
→ [Рекомендации]
```

### Пример 3: [Edge case или failure scenario]

**Input:**
```markdown
User: [Запрос]
Context: [Проблемная ситуация]
```

**Process:**
```
1. [Обнаружение проблемы]
2. [Попытка решения]
3. [Эскалация или альтернативный подход]
```

**Output:**
```
❌ [Описание проблемы]
→ [Эскалация или решение]
```

---

## ⚠️ ОСОБЫЕ СЛУЧАИ

### Failure Scenarios:

**1. [Название сценария провала]:**
- **Problem**: [Описание проблемы]
- **Solution**: [Как обрабатывать]
- **Escalation**: [Когда эскалировать к пользователю]
- **Format**:
  ```markdown
  ❌ [ERROR TITLE]

  [Описание проблемы]

  REQUIRED ACTION:
  - [Действие 1]
  - [Действие 2]
  ```

**2. [Другой сценарий провала]:**
- **Problem**: [Описание]
- **Solution**: [Как обрабатывать]
- **Prevention**: [Как предотвратить в будущем]

### Edge Cases:

**[Нестандартный случай 1]:**
```
[Описание случая]
```
- **Condition**: [Когда возникает]
- **Solution**: [Как адаптировать workflow]
- **Example**: [Конкретный пример]

**[Нестандартный случай 2]:**
```
[Описание случая]
```
- **Condition**: [Когда возникает]
- **Solution**: [Как адаптировать workflow]
- **Example**: [Конкретный пример]

---

## 📚 ССЫЛКИ

**MANDATORY Reading:**
- [rule-name.mdc](../../.cursor/rules/rule-name.mdc) - **CRITICAL rules**

**Связанные агенты:**
- agent-name (связь с этим агентом)
- another-agent (связь с этим агентом)
- third-agent (связь с этим агентом)

**Правила:**
- [common-rule.mdc](../../.cursor/rules/common-rule.mdc)
- [another-rule.mdc](../../.cursor/rules/another-rule.mdc)

**Инструменты (если применимо):**
- [script-name.ps1](../../.cursor/tools/script-name.ps1)
- [another-script.ps1](../../.cursor/tools/another-script.ps1)

---

**Модель:** [sonnet/opus/haiku] ([краткое описание почему эта модель])
**Цвет:** [color] ([описание фазы или типа работы])
**Статус:** ✅ [Описание текущего статуса агента]
