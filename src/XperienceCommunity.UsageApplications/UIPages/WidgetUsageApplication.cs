using CMS.Membership;
using Kentico.Xperience.Admin.Base;
using XperienceCommunity.UsageApplications;
using XperienceCommunity.UsageApplications.UIPages;

[assembly: UIApplication(
    identifier: WidgetUsageApplication.IDENTIFIER,
    type: typeof(WidgetUsageApplication),
    slug: "widget-usage",
    name: "Widget Usage",
    category: UsageApplicationsAdminModule.CATEGORY,
    icon: Icons.Boxes,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace XperienceCommunity.UsageApplications.UIPages;

/// <summary>
/// Application that reports how many times each page builder widget is used across the site content.
/// </summary>
[UIPermission(SystemPermissions.VIEW)]
public class WidgetUsageApplication : ApplicationPage
{
    // Unique identifier of the application
    public const string IDENTIFIER = "XperienceCommunity.UsageApplications.Application.WidgetUsage";
}
