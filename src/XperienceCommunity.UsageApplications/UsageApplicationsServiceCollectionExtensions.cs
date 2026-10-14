using Microsoft.Extensions.DependencyInjection;
using XperienceCommunity.UsageApplications.Features.ContentTypeUsage;
using XperienceCommunity.UsageApplications.Features.WidgetUsage;

namespace XperienceCommunity.UsageApplications;

/// <summary>
/// Extension methods for registering the Content Type Usage and Widget Usage admin applications.
/// </summary>
public static class UsageApplicationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services backing the Content Type Usage and Widget Usage admin applications.
    /// </summary>
    public static IServiceCollection AddUsageApplications(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IContentTypeUsageService, ContentTypeUsageService>();
        services.AddScoped<IWidgetUsageService, WidgetUsageService>();

        return services;
    }
}
