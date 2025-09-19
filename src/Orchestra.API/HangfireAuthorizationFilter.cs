using Hangfire.Dashboard;

namespace Orchestra.API;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // For development/local environment, allow all access
        // In production, implement proper authentication/authorization
        var httpContext = context.GetHttpContext();

        // Allow access from localhost only for security
        var request = httpContext.Request;
        var isLocalhost = request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         request.Host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                         request.Host.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);

        return isLocalhost;
    }
}