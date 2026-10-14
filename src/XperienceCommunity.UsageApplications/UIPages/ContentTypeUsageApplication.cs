using CMS.Membership;
using Kentico.Xperience.Admin.Base;
using XperienceCommunity.UsageApplications;
using XperienceCommunity.UsageApplications.UIPages;

[assembly: UIApplication(
    identifier: ContentTypeUsageApplication.IDENTIFIER,
    type: typeof(ContentTypeUsageApplication),
    slug: "content-type-usage",
    name: "Content Type Usage",
    category: UsageApplicationsAdminModule.CATEGORY,
    icon: Icons.Table,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace XperienceCommunity.UsageApplications.UIPages;

/// <summary>
/// Application that reports how many content items exist for each content type and links to the items.
/// </summary>
[UIPermission(SystemPermissions.VIEW)]
public class ContentTypeUsageApplication : ApplicationPage
{
    // Unique identifier of the application
    public const string IDENTIFIER = "XperienceCommunity.UsageApplications.Application.ContentTypeUsage";
}
