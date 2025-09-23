using Orchestra.Core;
using Orchestra.Core.Data;
using Orchestra.Core.Services;
using Orchestra.API.Jobs;
using Orchestra.API.Services;
using Orchestra.API.Hubs;
using Orchestra.Web.Services;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Storage.SQLite;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Orchestra.API;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        services.AddOpenApi();
        // Add configuration for CORS origins
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .Build();

        services.AddCors(options =>
        {
            options.AddPolicy("BlazorWasmPolicy", builder =>
            {
                var allowedOrigins = configuration.GetSection("Cors:BlazorOrigins").Get<string[]>()
                    ?? new[] { "https://localhost:5001", "http://localhost:5000" };

                builder
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithHeaders("Authorization", "Content-Type", "x-signalr-user-agent")
                    .SetIsOriginAllowed(origin => true); // Allow dynamic origins for development
            });
        });

        // Database connection string for SQLite
        // Use unique database for tests to avoid conflicts
        var dbFileName = Environment.GetEnvironmentVariable("HANGFIRE_CONNECTION") ?? "orchestra.db";
        var connectionString = $"Data Source={dbFileName}";

        // Configure Entity Framework DbContext
        services.AddDbContext<OrchestraDbContext>(options =>
        {
            options.UseSqlite(connectionString, b => b.MigrationsAssembly("Orchestra.API"));
            options.EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
            options.EnableDetailedErrors(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
        });

        // Hangfire configuration
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSQLiteStorage(connectionString, new SQLiteStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5)
            }));

        services.AddHangfireServer(options =>
        {
            options.Queues = new[] { "default", "high-priority", "long-running", "maintenance" };
            options.WorkerCount = Environment.ProcessorCount * 2;
        });

        // Add SignalR services for real-time agent communication
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            options.StreamBufferCapacity = 10;
        });

        services.AddSingleton<SimpleOrchestrator>();
        services.AddSingleton<AgentConfiguration>(provider =>
            AgentConfiguration.LoadFromFile("agent-config.json"));
        services.AddHostedService<AgentScheduler>();
        services.AddHostedService<BackgroundTaskAssignmentService>();

        // Register Hangfire job classes
        services.AddScoped<TaskExecutionJob>();

        // Register HangfireOrchestrator - the critical integration bridge
        services.AddScoped<HangfireOrchestrator>();

        // Register Agent Executor - configurable agent implementation
        // Use ClaudeAgentExecutor for real Claude Code integration, SimulationAgentExecutor for testing
        services.AddScoped<IAgentExecutor, ClaudeAgentExecutor>();
        // Alternative for testing/fallback: services.AddScoped<IAgentExecutor, SimulationAgentExecutor>();

        // Register Web services for batch task execution
        services.AddHttpClient(); // Required for OrchestratorService
        services.AddScoped<IOrchestratorService, OrchestratorService>();
        services.AddScoped<IDependencyGraphBuilder, DependencyGraphBuilder>();
        services.AddScoped<ITaskExecutionEngine, TaskExecutionEngine>();
        services.AddScoped<BatchTaskExecutor>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors("BlazorWasmPolicy");

        // Enable static files (for coordinator.html)
        app.UseStaticFiles();

        // Hangfire Dashboard with basic authorization
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() }
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            // Map SignalR hubs for real-time communication
            endpoints.MapHub<AgentCommunicationHub>("/agentHub");
            endpoints.MapHub<CoordinatorChatHub>("/coordinatorHub");
        });

        // Schedule recurring jobs
        RecurringJob.AddOrUpdate(
            "task-cleanup",
            () => Console.WriteLine("Task cleanup job executed at " + DateTime.UtcNow),
            Cron.Hourly);
    }
}