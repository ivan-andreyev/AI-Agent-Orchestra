using System.Text.RegularExpressions;

namespace Orchestra.Core.Services;

/// <summary>
/// Читает и парсит markdown планы работ агентов
/// </summary>
public class MarkdownPlanReader
{
    private readonly string _plansDirectory;

    public MarkdownPlanReader(string plansDirectory = "agent-plans")
    {
        _plansDirectory = plansDirectory;
    }

    /// <summary>
    /// Получает активные планы для всех агентов
    /// </summary>
    public List<AgentWorkPlan> GetActivePlans()
    {
        var plans = new List<AgentWorkPlan>();
        var activePlansDir = Path.Combine(_plansDirectory, "active");

        if (!Directory.Exists(activePlansDir))
        {
            return plans;
        }

        var markdownFiles = Directory.GetFiles(activePlansDir, "*.md");

        foreach (var file in markdownFiles)
        {
            try
            {
                var plan = ParseMarkdownPlan(file);
                if (plan != null)
                {
                    plans.Add(plan);
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но продолжаем обработку других файлов
                Console.WriteLine($"Error parsing plan {file}: {ex.Message}");
            }
        }

        return plans;
    }

    /// <summary>
    /// Получает план для конкретного агента
    /// </summary>
    public AgentWorkPlan? GetPlanForAgent(string agentId)
    {
        return GetActivePlans().FirstOrDefault(p => p.AgentId == agentId);
    }

    /// <summary>
    /// Парсит markdown файл в объект плана работ
    /// </summary>
    private AgentWorkPlan? ParseMarkdownPlan(string filePath)
    {
        var content = File.ReadAllText(filePath);

        // Извлекаем метаданные из заголовка
        var agentId = ExtractField(content, "Agent");
        var status = ExtractField(content, "Status");
        var started = ExtractField(content, "Started");
        var repository = ExtractField(content, "Repository");

        if (string.IsNullOrEmpty(agentId))
        {
            return null;
        }

        // Извлекаем цель
        var goal = ExtractSection(content, "Goal");

        // Извлекаем текущую задачу
        var currentTask = ExtractSection(content, "Current Task");

        // Подсчитываем прогресс по чекбоксам
        var (completed, total) = CountTaskProgress(content);
        var progressPercent = total > 0 ? (completed * 100) / total : 0;

        // Извлекаем последний пинг
        var lastPingStr = ExtractField(content, "Last Ping");
        DateTime? lastPing = null;
        if (DateTime.TryParse(lastPingStr, out var pingDate))
        {
            lastPing = pingDate;
        }

        return new AgentWorkPlan(
            Path.GetFileNameWithoutExtension(filePath),
            agentId,
            status ?? "unknown",
            goal ?? "",
            currentTask ?? "",
            progressPercent,
            lastPing,
            repository ?? "",
            filePath
        );
    }

    /// <summary>
    /// Извлекает значение поля из markdown
    /// </summary>
    private string? ExtractField(string content, string fieldName)
    {
        var pattern = $@"\*\*{fieldName}\*\*:\s*(.+)";
        var match = Regex.Match(content, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Извлекает секцию из markdown
    /// </summary>
    private string? ExtractSection(string content, string sectionName)
    {
        var pattern = $@"## {sectionName}\s*\n(.*?)(?=\n## |\n# |$)";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Подсчитывает прогресс по чекбоксам в markdown
    /// </summary>
    private (int completed, int total) CountTaskProgress(string content)
    {
        var completedPattern = @"- \[x\]";
        var incompletePattern = @"- \[ \]";

        var completed = Regex.Matches(content, completedPattern).Count;
        var incomplete = Regex.Matches(content, incompletePattern).Count;

        return (completed, completed + incomplete);
    }

    /// <summary>
    /// Обновляет прогресс в markdown файле
    /// </summary>
    public void UpdatePlanProgress(string agentId, string progressNote)
    {
        var plan = GetPlanForAgent(agentId);
        if (plan == null) return;

        var content = File.ReadAllText(plan.FilePath);

        // Добавляем заметку о прогрессе
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var newNote = $"\n### {timestamp}\n- {progressNote}\n";

        // Ищем секцию Progress Notes
        var progressSectionPattern = @"(## Progress Notes\s*)(.*?)((?=\n## |\n# |$))";
        var match = Regex.Match(content, progressSectionPattern, RegexOptions.Singleline);

        if (match.Success)
        {
            var before = content.Substring(0, match.Index + match.Groups[1].Length);
            var existing = match.Groups[2].Value;
            var after = content.Substring(match.Index + match.Length - match.Groups[3].Length);

            content = before + existing + newNote + match.Groups[3].Value + after;
        }

        // Обновляем Last Ping
        content = Regex.Replace(content,
            @"(\*\*Last Ping\*\*:\s*)([^\n]*)",
            $"$1{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");

        File.WriteAllText(plan.FilePath, content);
    }
}

/// <summary>
/// Представляет план работ агента
/// </summary>
public record AgentWorkPlan(
    string PlanName,
    string AgentId,
    string Status,
    string Goal,
    string CurrentTask,
    int ProgressPercent,
    DateTime? LastPing,
    string Repository,
    string FilePath
);