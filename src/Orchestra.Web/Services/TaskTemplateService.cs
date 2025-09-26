using Orchestra.Web.Models;
using Orchestra.Core.Models;
using System.Text.Json;
using Microsoft.JSInterop;
using TaskPriority = Orchestra.Core.Models.TaskPriority;

namespace Orchestra.Web.Services;

/// <summary>
/// Service for managing task templates including CRUD operations and execution planning
/// </summary>
public class TaskTemplateService
{
    private readonly ILogger<TaskTemplateService> _logger;
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, TaskTemplate> _templateCache;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string StorageKey = "orchestra_task_templates";

    public TaskTemplateService(ILogger<TaskTemplateService> logger, IJSRuntime jsRuntime)
    {
        _logger = logger;
        _jsRuntime = jsRuntime;
        _templateCache = new Dictionary<string, TaskTemplate>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        // Initialize with default templates
        InitializeDefaultTemplates();
    }

    /// <summary>
    /// Получить список шаблонов с опциональной фильтрацией по категории
    /// </summary>
    public async Task<List<TaskTemplate>> GetTemplatesAsync(TaskCategory? category = null)
    {
        try
        {
            await LoadTemplatesFromStorageAsync();
            var templates = _templateCache.Values.ToList();

            if (category.HasValue)
            {
                templates = templates.Where(t => t.Category == category.Value).ToList();
            }

            _logger.LogDebug("Retrieved {Count} templates for category {Category}", templates.Count, category);
            return templates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for category {Category}", category);
            return new List<TaskTemplate>();
        }
    }

    /// <summary>
    /// Получить конкретный шаблон по идентификатору
    /// </summary>
    public async Task<TaskTemplate?> GetTemplateAsync(string templateId)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return null;
        }

        try
        {
            await LoadTemplatesFromStorageAsync();

            if (_templateCache.TryGetValue(templateId, out var template))
            {
                return template;
            }

            _logger.LogWarning("Template {TemplateId} not found", templateId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateId}", templateId);
            return null;
        }
    }

    /// <summary>
    /// Сохранить шаблон в хранилище
    /// </summary>
    public async Task SaveTemplateAsync(TaskTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        try
        {
            if (!await ValidateTemplateAsync(template))
            {
                throw new TemplateValidationException($"Template {template.Id} validation failed");
            }

            _templateCache[template.Id] = template;
            await SaveTemplatesToStorageAsync();

            _logger.LogInformation("Template {TemplateId} saved successfully", template.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving template {TemplateId}", template.Id);
            throw;
        }
    }

    /// <summary>
    /// Удалить шаблон из хранилища
    /// </summary>
    public async Task DeleteTemplateAsync(string templateId)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));
        }

        try
        {
            await LoadTemplatesFromStorageAsync();

            if (_templateCache.Remove(templateId))
            {
                await SaveTemplatesToStorageAsync();
                _logger.LogInformation("Template {TemplateId} deleted successfully", templateId);
            }
            else
            {
                _logger.LogWarning("Template {TemplateId} not found for deletion", templateId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", templateId);
            throw;
        }
    }

    /// <summary>
    /// Построить план выполнения для шаблона с параметрами
    /// </summary>
    public async Task<ExecutionPlan> BuildExecutionPlanAsync(string templateId, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            throw new ArgumentException("Template ID cannot be null or empty", nameof(templateId));
        }

        parameters ??= new Dictionary<string, object>();

        try
        {
            var template = await GetTemplateAsync(templateId);
            if (template == null)
            {
                throw new TemplateNotFoundException($"Template {templateId} not found");
            }

            // Validate parameters against template schema
            ValidateParameters(template, parameters);

            // Build execution steps with parameter substitution
            var executionSteps = new List<ExecutionStep>();

            foreach (var step in template.Steps)
            {
                var resolvedCommand = SubstituteParameters(step.Command, parameters, template.DefaultParameters);
                var executionStep = new ExecutionStep(
                    Command: resolvedCommand,
                    Priority: step.Priority,
                    RequiresPreviousSuccess: step.RequiresPreviousSuccess,
                    EstimatedDuration: step.EstimatedDuration ?? TimeSpan.FromMinutes(1),
                    StepIndex: executionSteps.Count
                );

                executionSteps.Add(executionStep);
            }

            var totalEstimatedDuration = executionSteps.Sum(s => s.EstimatedDuration.TotalSeconds);

            var executionPlan = new ExecutionPlan(
                TemplateId: templateId,
                TemplateName: template.Name,
                Steps: executionSteps,
                TotalEstimatedDuration: TimeSpan.FromSeconds(totalEstimatedDuration),
                Parameters: parameters
            );

            _logger.LogInformation("Built execution plan for template {TemplateId} with {StepCount} steps",
                templateId, executionSteps.Count);

            return executionPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building execution plan for template {TemplateId}", templateId);
            throw;
        }
    }

    /// <summary>
    /// Валидация шаблона
    /// </summary>
    public async Task<bool> ValidateTemplateAsync(TaskTemplate template)
    {
        if (template == null)
        {
            return false;
        }

        try
        {
            // Basic validation
            if (string.IsNullOrEmpty(template.Id) || string.IsNullOrEmpty(template.Name))
            {
                return false;
            }

            // Validate steps
            if (template.Steps == null || !template.Steps.Any())
            {
                return false;
            }

            foreach (var step in template.Steps)
            {
                if (string.IsNullOrEmpty(step.Command))
                {
                    return false;
                }
            }

            // Check for circular dependencies (simplified check)
            if (HasCircularDependencies(template))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template {TemplateId}", template.Id);
            return false;
        }
    }

    /// <summary>
    /// Импорт шаблона из JSON
    /// </summary>
    public async Task ImportTemplateAsync(string templateJson)
    {
        if (string.IsNullOrEmpty(templateJson))
        {
            throw new ArgumentException("Template JSON cannot be null or empty", nameof(templateJson));
        }

        try
        {
            var template = JsonSerializer.Deserialize<TaskTemplate>(templateJson, _jsonOptions);
            if (template == null)
            {
                throw new TemplateValidationException("Failed to deserialize template JSON");
            }

            await SaveTemplateAsync(template);
            _logger.LogInformation("Template {TemplateId} imported successfully", template.Id);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON format for template import");
            throw new TemplateValidationException("Invalid JSON format", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing template");
            throw;
        }
    }

    /// <summary>
    /// Экспорт шаблона в JSON
    /// </summary>
    public async Task<string> ExportTemplateAsync(string templateId)
    {
        var template = await GetTemplateAsync(templateId);
        if (template == null)
        {
            throw new TemplateNotFoundException($"Template {templateId} not found");
        }

        try
        {
            return JsonSerializer.Serialize(template, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting template {TemplateId}", templateId);
            throw;
        }
    }

    private async Task LoadTemplatesFromStorageAsync()
    {
        try
        {
            var templatesJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);

            if (!string.IsNullOrEmpty(templatesJson))
            {
                var templates = JsonSerializer.Deserialize<List<TaskTemplate>>(templatesJson, _jsonOptions);
                if (templates != null)
                {
                    _templateCache.Clear();
                    foreach (var template in templates)
                    {
                        _templateCache[template.Id] = template;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading templates from storage");
        }
    }

    private async Task SaveTemplatesToStorageAsync()
    {
        try
        {
            var templates = _templateCache.Values.ToList();
            var templatesJson = JsonSerializer.Serialize(templates, _jsonOptions);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, templatesJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving templates to storage");
            throw;
        }
    }

    private void InitializeDefaultTemplates()
    {
        var defaultTemplates = new List<TaskTemplate>
        {
            new TaskTemplate(
                Id: "dotnet-ci-pipeline",
                Name: ".NET CI Pipeline",
                Description: "Complete CI/CD pipeline for .NET projects",
                Category: TaskCategory.Development,
                Steps: new List<TaskStep>
                {
                    new TaskStep("git pull", TaskPriority.Normal, false, TimeSpan.FromSeconds(30), new Dictionary<string, string>()),
                    new TaskStep("dotnet restore", TaskPriority.Normal, true, TimeSpan.FromMinutes(1), new Dictionary<string, string>()),
                    new TaskStep("dotnet build --configuration {{configuration}}", TaskPriority.Normal, true, TimeSpan.FromMinutes(2), new Dictionary<string, string>()),
                    new TaskStep("dotnet test --verbosity {{verbosity}}", TaskPriority.High, true, TimeSpan.FromMinutes(3), new Dictionary<string, string>())
                },
                DefaultParameters: new Dictionary<string, object>
                {
                    { "configuration", "Release" },
                    { "verbosity", "minimal" }
                },
                AllowParameterCustomization: true
            ),
            new TaskTemplate(
                Id: "code-quality-audit",
                Name: "Code Quality Audit",
                Description: "Comprehensive code quality analysis",
                Category: TaskCategory.Analysis,
                Steps: new List<TaskStep>
                {
                    new TaskStep("Analyze code quality and suggest improvements", TaskPriority.Normal, false, TimeSpan.FromMinutes(5), new Dictionary<string, string>()),
                    new TaskStep("Find and fix potential security issues", TaskPriority.High, false, TimeSpan.FromMinutes(10), new Dictionary<string, string>()),
                    new TaskStep("Identify performance bottlenecks", TaskPriority.Normal, false, TimeSpan.FromMinutes(8), new Dictionary<string, string>())
                },
                DefaultParameters: new Dictionary<string, object>(),
                AllowParameterCustomization: false
            ),
            new TaskTemplate(
                Id: "documentation-update",
                Name: "Documentation Update",
                Description: "Update project documentation",
                Category: TaskCategory.Documentation,
                Steps: new List<TaskStep>
                {
                    new TaskStep("Update README.md with current project state", TaskPriority.Normal, false, TimeSpan.FromMinutes(5), new Dictionary<string, string>()),
                    new TaskStep("Generate API documentation", TaskPriority.Normal, false, TimeSpan.FromMinutes(10), new Dictionary<string, string>()),
                    new TaskStep("Add missing code comments", TaskPriority.Low, false, TimeSpan.FromMinutes(15), new Dictionary<string, string>())
                },
                DefaultParameters: new Dictionary<string, object>(),
                AllowParameterCustomization: false
            )
        };

        foreach (var template in defaultTemplates)
        {
            _templateCache[template.Id] = template;
        }
    }

    private void ValidateParameters(TaskTemplate template, Dictionary<string, object> parameters)
    {
        // Basic parameter validation - can be extended
        if (template.AllowParameterCustomization)
        {
            // Check required parameters based on template commands
            var requiredParams = ExtractRequiredParameters(template);
            var missingParams = requiredParams.Where(p => !parameters.ContainsKey(p) && !template.DefaultParameters.ContainsKey(p)).ToList();

            if (missingParams.Any())
            {
                throw new MissingParameterException($"Missing required parameters: {string.Join(", ", missingParams)}");
            }
        }
    }

    private List<string> ExtractRequiredParameters(TaskTemplate template)
    {
        var requiredParams = new List<string>();

        foreach (var step in template.Steps)
        {
            // Simple regex to find {{parameter}} patterns
            var matches = System.Text.RegularExpressions.Regex.Matches(step.Command, @"\{\{(\w+)\}\}");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var paramName = match.Groups[1].Value;
                if (!requiredParams.Contains(paramName))
                {
                    requiredParams.Add(paramName);
                }
            }
        }

        return requiredParams;
    }

    private string SubstituteParameters(string command, Dictionary<string, object> parameters, Dictionary<string, object> defaultParameters)
    {
        var result = command;

        // Replace {{parameter}} patterns with actual values
        var matches = System.Text.RegularExpressions.Regex.Matches(command, @"\{\{(\w+)\}\}");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var paramName = match.Groups[1].Value;
            var value = "";

            if (parameters.TryGetValue(paramName, out var paramValue))
            {
                value = paramValue?.ToString() ?? "";
            }
            else if (defaultParameters.TryGetValue(paramName, out var defaultValue))
            {
                value = defaultValue?.ToString() ?? "";
            }

            result = result.Replace(match.Value, value);
        }

        return result;
    }

    private bool HasCircularDependencies(TaskTemplate template)
    {
        // Simplified check - for now just return false
        // In a real implementation, this would analyze step dependencies
        return false;
    }
}

// Template models
public record TaskTemplate(
    string Id,
    string Name,
    string Description,
    TaskCategory Category,
    List<TaskStep> Steps,
    Dictionary<string, object> DefaultParameters,
    bool AllowParameterCustomization
);

public record TaskStep(
    string Command,
    TaskPriority Priority,
    bool RequiresPreviousSuccess,
    TimeSpan? EstimatedDuration,
    Dictionary<string, string> ParameterMappings
);

public record ExecutionPlan(
    string TemplateId,
    string TemplateName,
    List<ExecutionStep> Steps,
    TimeSpan TotalEstimatedDuration,
    Dictionary<string, object> Parameters
);

public record ExecutionStep(
    string Command,
    TaskPriority Priority,
    bool RequiresPreviousSuccess,
    TimeSpan EstimatedDuration,
    int StepIndex
);

public enum TaskCategory
{
    Development,
    Analysis,
    Documentation,
    Maintenance,
    Custom
}

// Exception classes
public class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(string message) : base(message) { }
    public TemplateNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

public class TemplateValidationException : Exception
{
    public TemplateValidationException(string message) : base(message) { }
    public TemplateValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class MissingParameterException : Exception
{
    public MissingParameterException(string message) : base(message) { }
}