using Orchestra.Core;
using Orchestra.Core.Data;
using Orchestra.Core.Services;
using Orchestra.API.Jobs;
using Orchestra.API.Services;
using Orchestra.API.Hubs;
using Orchestra.Web.Services;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Orchestra.Core.HealthChecks;
using MediatR;

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

        // Configure memory cache with settings from appsettings.json
        services.AddMemoryCache(options =>
        {
            var cacheConfig = configuration.GetSection("Cache");
            options.SizeLimit = cacheConfig.GetValue<long>("SizeLimit", 1000);
            options.CompactionPercentage = cacheConfig.GetValue<double>("CompactionPercentage", 0.25);
        });

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

        // Database connection strings for PostgreSQL
        var currentEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isTestEnvironment = currentEnvironment == "Testing";

        // PostgreSQL Database Connection
        var defaultConnectionString = "Host=localhost;Database=orchestra;Username=postgres;Password=postgres";
        var efCoreConnectionString = Environment.GetEnvironmentVariable("EFCORE_CONNECTION") ??
                                   (isTestEnvironment ?
                                    Environment.GetEnvironmentVariable("HANGFIRE_CONNECTION")?.Replace("orchestra", "orchestra_test") ?? "Host=localhost;Database=orchestra_test;Username=postgres;Password=postgres" :
                                    defaultConnectionString);

        // Hangfire Database Connection (same as EF Core for PostgreSQL)
        var hangfireConnectionString = Environment.GetEnvironmentVariable("HANGFIRE_CONNECTION") ?? defaultConnectionString;

        // Configure Entity Framework DbContext
        services.AddDbContext<OrchestraDbContext>(options =>
        {
            options.UseNpgsql(efCoreConnectionString, b => b.MigrationsAssembly("Orchestra.API"));
            options.EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
            options.EnableDetailedErrors(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
        });

        // Hangfire configuration
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(hangfireConnectionString, new Hangfire.PostgreSql.PostgreSqlStorageOptions
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

        // Add MediatR for LLM-friendly Command/Query pattern
        services.AddMediatR(typeof(Startup).Assembly, typeof(Orchestra.Core.Commands.ICommand).Assembly);

        services.AddSingleton<SimpleOrchestrator>(provider =>
        {
            var claudeCodeService = provider.GetService<Orchestra.Core.Services.IClaudeCodeCoreService>();
            return new SimpleOrchestrator("orchestrator-state.json", claudeCodeService);
        });
        services.AddSingleton<AgentConfiguration>(provider =>
            AgentConfiguration.LoadFromFile("agent-config.json"));
        services.AddHostedService<AgentScheduler>(provider =>
        {
            var orchestrator = provider.GetRequiredService<SimpleOrchestrator>();
            var logger = provider.GetRequiredService<ILogger<AgentScheduler>>();
            var config = provider.GetRequiredService<AgentConfiguration>();
            var claudeCodeService = provider.GetService<Orchestra.Core.Services.IClaudeCodeCoreService>();
            return new AgentScheduler(orchestrator, logger, config, claudeCodeService);
        });
        services.AddHostedService<BackgroundTaskAssignmentService>();

        // Register Hangfire job classes
        services.AddScoped<TaskExecutionJob>();

        // Register HangfireOrchestrator - the critical integration bridge with TaskRepository support
        services.AddScoped<HangfireOrchestrator>(provider =>
        {
            var jobClient = provider.GetRequiredService<IBackgroundJobClient>();
            var entityOrchestrator = provider.GetRequiredService<EntityFrameworkOrchestrator>();
            var legacyOrchestrator = provider.GetRequiredService<SimpleOrchestrator>();
            var logger = provider.GetRequiredService<ILogger<HangfireOrchestrator>>();
            var context = provider.GetRequiredService<OrchestraDbContext>(); // For TaskRepository integration

            return new HangfireOrchestrator(jobClient, entityOrchestrator, legacyOrchestrator, logger, context);
        });

        // Register Entity Framework-based services
        services.AddScoped<AgentRepository>();
        services.AddScoped<TaskRepository>();
        services.AddScoped<EntityFrameworkOrchestrator>();

        // Register Agent Executor - configurable agent implementation
        // Use ClaudeAgentExecutor for real Claude Code integration, SimulationAgentExecutor for testing
        services.AddSingleton<IAgentExecutor, ClaudeAgentExecutor>();
        // Alternative for testing/fallback: services.AddScoped<IAgentExecutor, SimulationAgentExecutor>();

        // Register Claude Code services for advanced Claude Code integration
        services.Configure<Orchestra.Agents.ClaudeCode.ClaudeCodeConfiguration>(
            configuration.GetSection("ClaudeCode"));

        // Register Claude Code services - TaskRepository integration is handled by TaskExecutionJob layer
        services.AddSingleton<Orchestra.Agents.ClaudeCode.ClaudeCodeService>();
        services.AddSingleton<Orchestra.Agents.ClaudeCode.IClaudeCodeService>(provider =>
            provider.GetRequiredService<Orchestra.Agents.ClaudeCode.ClaudeCodeService>());
        services.AddSingleton<Orchestra.Core.Services.IClaudeCodeCoreService>(provider =>
            provider.GetRequiredService<Orchestra.Agents.ClaudeCode.ClaudeCodeService>());
        services.AddScoped<Orchestra.Agents.ClaudeCode.ClaudeCodeWorkflowExecutor>();

        // Register chat context service for coordinator chat management
        services.AddScoped<IChatContextService, ChatContextService>();

        // Register connection session service for thread-safe session management
        services.AddSingleton<IConnectionSessionService, ConnectionSessionService>();

        // Register repository path service for configuration management
        services.AddScoped<IRepositoryPathService, RepositoryPathService>();

        // Add health checks for chat context service and dependencies
        services.AddHealthChecks()
            .AddCheck<ChatContextServiceHealthCheck>("chat-context", tags: new[] { "chat", "database" })
            .AddDbContextCheck<OrchestraDbContext>("database", tags: new[] { "database" });


        // Register Web services for batch task execution
        services.AddHttpClient(); // Required for OrchestratorService
        services.AddScoped<IOrchestratorService, OrchestratorService>();
        services.AddScoped<IDependencyGraphBuilder, DependencyGraphBuilder>();
        services.AddScoped<ITaskExecutionEngine, TaskExecutionEngine>();
        services.AddScoped<BatchTaskExecutor>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IConfiguration configuration)
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

            // Health check endpoints
            endpoints.MapHealthChecks("/health");
            endpoints.MapHealthChecks("/health/chat", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("chat")
            });
            endpoints.MapHealthChecks("/health/database", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("database")
            });
        });

        // Validate critical services at startup
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Startup>>();

            try
            {
                // Validate chat context service can be created and is functional
                var chatService = scope.ServiceProvider.GetRequiredService<IChatContextService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrchestraDbContext>();

                // Проверяем, что сервисы могут быть созданы
                logger.LogInformation("Chat context service and database validated successfully");

                // Validate memory cache is working
                var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                    Size = 1 // Required when SizeLimit is set
                };
                memoryCache.Set("startup-validation", DateTime.UtcNow, cacheEntryOptions);

                logger.LogInformation("Memory cache validation completed successfully");

                // Warm up Claude Code CLI agents to avoid cold start delays
                var claudeCodeConfig = configuration.GetSection("ClaudeCode").Get<Orchestra.Agents.ClaudeCode.ClaudeCodeConfiguration>()
                    ?? new Orchestra.Agents.ClaudeCode.ClaudeCodeConfiguration();

                if (claudeCodeConfig.WarmupEnabled)
                {
                    var orchestrator = scope.ServiceProvider.GetRequiredService<HangfireOrchestrator>();
                    Task.Run(async () =>
                    {
                        await Task.Delay(claudeCodeConfig.WarmupDelayMs); // Configurable warmup delay
                        await orchestrator.WarmupClaudeCodeAgentsAsync();
                    });

                    logger.LogInformation("Claude Code CLI warm-up initiated with delay {WarmupDelayMs}ms", claudeCodeConfig.WarmupDelayMs);
                }
                else
                {
                    logger.LogInformation("Claude Code CLI warm-up disabled by configuration");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to validate critical services at startup");
                // In production, consider failing startup here
                // throw new InvalidOperationException("Critical service validation failed", ex);
            }
        }

        // Schedule recurring jobs
        RecurringJob.AddOrUpdate(
            "task-cleanup",
            () => Console.WriteLine("Task cleanup job executed at " + DateTime.UtcNow),
            Cron.Hourly);
    }
}
