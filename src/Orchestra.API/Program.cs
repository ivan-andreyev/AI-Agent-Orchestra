using Orchestra.Core;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton<SimpleOrchestrator>();

// Загружаем конфигурацию агентов
var agentConfig = AgentConfiguration.LoadFromFile("agent-config.json");
builder.Services.AddSingleton(agentConfig);

// Добавляем фоновый сервис планировщика
builder.Services.AddHostedService<AgentScheduler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

// Agent Management
app.MapPost("/agents/register", (RegisterAgentRequest request, SimpleOrchestrator orchestrator) =>
{
    orchestrator.RegisterAgent(request.Id, request.Name, request.Type, request.RepositoryPath);
    return Results.Ok("Agent registered");
});

app.MapPost("/agents/{agentId}/ping", (string agentId, PingRequest request, SimpleOrchestrator orchestrator) =>
{
    orchestrator.UpdateAgentStatus(agentId, request.Status, request.CurrentTask);
    return Results.Ok("Agent status updated");
});

app.MapGet("/agents", (SimpleOrchestrator orchestrator) =>
{
    return orchestrator.GetAllAgents();
});

// Task Management
app.MapPost("/tasks/queue", (QueueTaskRequest request, SimpleOrchestrator orchestrator) =>
{
    orchestrator.QueueTask(request.Command, request.RepositoryPath, request.Priority);
    return Results.Ok("Task queued");
});

app.MapGet("/agents/{agentId}/next-task", (string agentId, SimpleOrchestrator orchestrator) =>
{
    var task = orchestrator.GetNextTaskForAgent(agentId);
    return task != null ? Results.Ok(task) : Results.NoContent();
});

// State
app.MapGet("/state", (SimpleOrchestrator orchestrator) =>
{
    return orchestrator.GetCurrentState();
});

app.Run();

record RegisterAgentRequest(string Id, string Name, string Type, string RepositoryPath);
record PingRequest(AgentStatus Status, string? CurrentTask);
record QueueTaskRequest(string Command, string RepositoryPath, TaskPriority Priority);
