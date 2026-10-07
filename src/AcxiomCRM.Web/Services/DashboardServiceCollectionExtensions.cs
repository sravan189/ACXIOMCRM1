using AcxiomCRM.Web.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the dashboard service. NOTE: this extension is NOT wired up in
/// Program.cs by the layout/dashboard worker (Program.cs is owned by another
/// worker). Add this one line next to the IAuditService registration:
/// <code>builder.Services.AddDashboard();</code>
/// </summary>
public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboard(this IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
