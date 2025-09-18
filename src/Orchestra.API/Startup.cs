using Orchestra.Core;
using Orchestra.Core.Services;
using System.Text.Json.Serialization;

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
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        services.AddSingleton<SimpleOrchestrator>();
        services.AddSingleton<AgentConfiguration>(provider =>
            AgentConfiguration.LoadFromFile("agent-config.json"));
        services.AddHostedService<AgentScheduler>();
        services.AddHostedService<BackgroundTaskAssignmentService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}