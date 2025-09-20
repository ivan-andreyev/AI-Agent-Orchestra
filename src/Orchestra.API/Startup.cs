using Orchestra.Core;
using Orchestra.Core.Services;
using Orchestra.API.Jobs;
using Orchestra.API.Services;
using Orchestra.Web.Services;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Storage.SQLite;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.SignalR;

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
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetIsOriginAllowed(origin => true); // Allow all origins with credentials for SignalR
            });
        });

        // Database connection string for SQLite
        // Use unique database for tests to avoid conflicts
        var connectionString = Environment.GetEnvironmentVariable("HANGFIRE_CONNECTION") ?? "orchestra.db";

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
        app.UseCors();

        // Hangfire Dashboard with basic authorization
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() }
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            // Map SignalR hub for real-time agent communication
            endpoints.MapHub<AgentCommunicationHub>("/agentHub");
        });

        // Schedule recurring jobs
        RecurringJob.AddOrUpdate(
            "task-cleanup",
            () => Console.WriteLine("Task cleanup job executed at " + DateTime.UtcNow),
            Cron.Hourly);
    }
}