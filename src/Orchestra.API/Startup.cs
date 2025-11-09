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
using Hangfire.InMemory;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Orchestra.Core.HealthChecks;
using MediatR;

namespace Orchestra.API;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        services.AddOpenApi();
        // Use injected configuration which includes test overrides
        var configuration = _configuration;

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

            // Add SignalR-specific CORS policy for agent interaction hub
            options.AddPolicy("SignalRPolicy", policy =>
            {
                var allowedOrigins = configuration.GetSection("Cors:BlazorOrigins").Get<string[]>()
                    ?? new[] { "https://localhost:5001", "http://localhost:5000" };

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });


        // Database connection configuration - support both PostgreSQL (production) and SQLite (testing)
        var currentEnvironment = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var isTestEnvironment = currentEnvironment == "Testing";
        
        // Detect if we're in testing mode with special configuration
        var efCoreConnection = Environment.GetEnvironmentVariable("EFCORE_CONNECTION") ?? configuration["EFCORE_CONNECTION"];
        var hangfireConnection = Environment.GetEnvironmentVariable("HANGFIRE_CONNECTION") ?? configuration["HANGFIRE_CONNECTION"];
        var useInMemoryHangfire = hangfireConnection == "InMemory";
        var useSqliteForHangfire = !string.IsNullOrEmpty(hangfireConnection) && hangfireConnection.StartsWith("Data Source=");
        var useSqliteForEfCore = isTestEnvironment && !string.IsNullOrEmpty(efCoreConnection) && !efCoreConnection.Contains("Host=");

        // PostgreSQL Database Connection (for production)
        var defaultConnectionString = "Host=localhost;Database=orchestra;Username=postgres;Password=postgres";
        
        // Configure Entity Framework DbContext
        services.AddDbContext<OrchestraDbContext>(options =>
        {
            if (useSqliteForEfCore)
            {
                // For tests: use SQLite with the specified database file
                var sqliteConnectionString = $"Data Source={efCoreConnection}";
                options.UseSqlite(sqliteConnectionString, b => b.MigrationsAssembly("Orchestra.API"));
            }
            else
            {
                // For production: use PostgreSQL
                var connectionString = efCoreConnection ?? defaultConnectionString;
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Orchestra.API"));
            }
            
            options.EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
            options.EnableDetailedErrors(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development");
        });

        // Hangfire configuration - conditional based on environment and connection string
        if (useInMemoryHangfire)
        {
            // For tests: use in-memory storage for faster, isolated execution
            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage());
        }
        else if (useSqliteForHangfire)
        {
            // For tests with isolation: use SQLite storage for independent test execution
            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSQLiteStorage(hangfireConnection, new Hangfire.Storage.SQLite.SQLiteStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromMilliseconds(100)
                }));
        }
        else
        {
            // For production: use PostgreSQL storage for persistence
            var hangfireConnectionString = hangfireConnection ?? defaultConnectionString;
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
        }

        services.AddHangfireServer(options =>
        {
            options.Queues = new[] { "default", "high-priority", "long-running", "maintenance" };
            options.WorkerCount = isTestEnvironment ? 1 : Environment.ProcessorCount * 2;
            options.SchedulePollingInterval = isTestEnvironment ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(15);
        });

        // Add SignalR services for real-time agent communication
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            options.StreamBufferCapacity = 100;
        });

        // Register AgentInteractionHub as Scoped for dependency injection
        services.AddScoped<AgentInteractionHub>();

        // Register AgentEventBroadcaster as HostedService for broadcasting session events to SignalR clients
        services.AddHostedService<Orchestra.API.Services.AgentEventBroadcaster>();

        // Add MediatR for LLM-friendly Command/Query pattern
        services.AddMediatR(typeof(Startup).Assembly, typeof(Orchestra.Core.Commands.ICommand).Assembly);

        // Register agent state store abstraction for proper isolation
        services.AddSingleton<Orchestra.Core.Abstractions.IAgentStateStore, Orchestra.Core.Services.InMemoryAgentStateStore>();

        // Register AgentSessionManager for managing external agent connections (Phase 1.3)
        services.AddSingleton<Orchestra.Core.Services.Connectors.IAgentSessionManager, Orchestra.Core.Services.Connectors.AgentSessionManager>();

        // Register AgentOutputBuffer for buffering agent output (required by TerminalAgentConnector)
        services.AddTransient<Orchestra.Core.Services.Connectors.IAgentOutputBuffer, Orchestra.Core.Services.Connectors.AgentOutputBuffer>();

        // Register ProcessDiscoveryService for discovering Claude Code processes
        services.AddSingleton<Orchestra.Core.Services.IProcessDiscoveryService, Orchestra.Core.Services.ProcessDiscoveryService>();

        services.AddTransient<Orchestra.Core.Services.Connectors.IAgentConnector, Orchestra.Core.Services.Connectors.TerminalAgentConnector>();

        // Register SimpleOrchestrator as Scoped with proper DI for agent state management
        services.AddSingleton<SimpleOrchestrator>(provider =>
        {
            var agentStateStore = provider.GetRequiredService<Orchestra.Core.Abstractions.IAgentStateStore>();
            var claudeCodeService = provider.GetService<Orchestra.Core.Services.IClaudeCodeCoreService>();
            var logger = provider.GetRequiredService<ILogger<SimpleOrchestrator>>();
            return new SimpleOrchestrator(agentStateStore, claudeCodeService, "orchestrator-state.json", logger);
        });
        services.AddSingleton<AgentConfiguration>(provider =>
            AgentConfiguration.LoadFromFile("agent-config.json"));
        
        // Only register background services for non-test environments
        // Background services cause DI issues in tests because they try to resolve scoped services from root provider
        if (!isTestEnvironment)
        {
            services.AddHostedService<AgentScheduler>(provider =>
            {
                var orchestrator = provider.GetRequiredService<SimpleOrchestrator>();
                var logger = provider.GetRequiredService<ILogger<AgentScheduler>>();
                var config = provider.GetRequiredService<AgentConfiguration>();
                var claudeCodeService = provider.GetService<Orchestra.Core.Services.IClaudeCodeCoreService>();
                return new AgentScheduler(orchestrator, logger, config, claudeCodeService);
            });
            services.AddHostedService<BackgroundTaskAssignmentService>();

            // Register background services for agent management
            services.AddHostedService<Orchestra.Core.Services.AgentHealthCheckService>();
            services.AddHostedService<Orchestra.Core.Services.AgentDiscoveryService>();
        }
        // Register Hangfire job classes
        services.AddScoped<TaskExecutionJob>();

        // Register HangfireOrchestrator - the critical integration bridge with TaskRepository support
        services.AddScoped<HangfireOrchestrator>(provider =>
        {
            var jobClient = provider.GetRequiredService<IBackgroundJobClient>();
            var entityOrchestrator = provider.GetRequiredService<EntityFrameworkOrchestrator>();
            var legacyOrchestrator = provider.GetRequiredService<SimpleOrchestrator>();
            var logger = provider.GetRequiredService<ILogger<HangfireOrchestrator>>();
            var taskRepository = provider.GetRequiredService<TaskRepository>();
            var agentRepository = provider.GetRequiredService<AgentRepository>();

            // Enable auto-agent creation for production (default true)
            return new HangfireOrchestrator(jobClient, entityOrchestrator, legacyOrchestrator, logger, taskRepository, agentRepository, autoCreateAgents: true);
        });

        // Register Entity Framework-based services
        services.AddScoped<AgentRepository>();
        services.AddScoped<TaskRepository>();
        services.AddScoped<EntityFrameworkOrchestrator>();

        // Register ClaudeSessionDiscovery для чтения истории из .jsonl файлов
        services.AddSingleton<ClaudeSessionDiscovery>();

        // Register chat and session services required by TaskExecutionJob
        services.AddScoped<IChatContextService, ChatContextService>();
        services.AddScoped<IConnectionSessionService, ConnectionSessionService>();

        // Register Retry Policy for agent executors
        services.AddSingleton<Orchestra.Core.Services.Retry.IRetryPolicy>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<Orchestra.Core.Services.Retry.ExponentialBackoffRetryPolicy>>();
            var claudeConfig = provider.GetRequiredService<IOptions<Orchestra.Agents.ClaudeCode.ClaudeCodeConfiguration>>().Value;

            return new Orchestra.Core.Services.Retry.ExponentialBackoffRetryPolicy(
                logger,
                maxAttempts: claudeConfig.RetryAttempts,
                baseDelay: claudeConfig.RetryDelay);
        });

        // Register Agent Executor - configurable agent implementation
        // Use ClaudeCodeExecutor for comprehensive Claude Code integration with retry logic
        services.AddSingleton<Orchestra.Agents.ClaudeCode.ClaudeCodeExecutor>();
        services.AddSingleton<IAgentExecutor>(provider =>
            provider.GetRequiredService<Orchestra.Agents.ClaudeCode.ClaudeCodeExecutor>());
        // Alternative implementations:
        // - ClaudeAgentExecutor: Simpler implementation (legacy)
        // - SimulationAgentExecutor: For testing/fallback

        // Register Agent Configuration Infrastructure (SRP pattern)
        services.AddSingleton<Orchestra.Core.Services.IAgentConfigurationRegistry,
            Orchestra.Core.Services.AgentConfigurationRegistry>();
        services.AddSingleton<Orchestra.Core.Services.IAgentConfigurationValidator,
            Orchestra.Core.Services.AgentConfigurationValidator>();
        services.AddSingleton<Orchestra.Core.Services.IAgentConfigurationFactory,
            Orchestra.Core.Services.AgentConfigurationFactory>();

        // Register Claude Code configuration
        services.Configure<Orchestra.Agents.ClaudeCode.ClaudeCodeConfiguration>(
            configuration.GetSection("ClaudeCode"));

        // Register ClaudeCodeConfiguration as IAgentConfiguration for factory
        services.AddSingleton<Orchestra.Core.Services.IAgentConfiguration>(provider =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Orchestra.Agents.ClaudeCode.ClaudeCodeConfiguration>>().Value);

        // Configure background services options
        services.Configure<Orchestra.Core.Services.AgentHealthCheckOptions>(
            configuration.GetSection("AgentHealthCheck"));
        services.Configure<Orchestra.Core.Services.AgentDiscoveryOptions>(
            configuration.GetSection("AgentDiscovery"));

        // Configure Terminal Connector options (for Agent Interaction System)
        services.Configure<Orchestra.Core.Services.Connectors.TerminalConnectorOptions>(
            configuration.GetSection("TerminalConnector"));

        // Register Terminal Agent Connector (transient for per-session isolation)
        services.AddTransient<Orchestra.Core.Services.Connectors.TerminalAgentConnector>();

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

        // Register message sequence service for thread-safe message ordering in concurrent environment
        services.AddSingleton<IMessageSequenceService, MessageSequenceService>();

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

        // Register TelegramEscalationService for permission escalation (Phase 3)
        services.Configure<TelegramEscalationOptions>(configuration.GetSection("TelegramEscalation"));
        services.AddSingleton<ITelegramEscalationService, TelegramEscalationService>();

        // Register PermissionDenialDetectionService for detecting permission_denials in Claude Code responses
        services.AddSingleton<IPermissionDenialDetectionService, PermissionDenialDetectionService>();
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
            endpoints.MapHub<AgentCommunicationHub>("/agentHub").RequireCors("BlazorWasmPolicy");
            endpoints.MapHub<CoordinatorChatHub>("/coordinatorHub").RequireCors("BlazorWasmPolicy");
            endpoints.MapHub<AgentInteractionHub>("/hubs/agent-interaction").RequireCors("SignalRPolicy");

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

        // Schedule recurring jobs only in non-testing environments
        var currentEnvironment = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        if (currentEnvironment != "Testing")
        {
            RecurringJob.AddOrUpdate(
                "task-cleanup",
                () => Console.WriteLine("Task cleanup job executed at " + DateTime.UtcNow),
                Cron.Hourly);
        }
    }
}
