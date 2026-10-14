using Kentico.Xperience.Admin.Base;
using XperienceCommunity.UsageApplications;

[assembly: CMS.AssemblyDiscoverable]
[assembly: CMS.RegisterModule(typeof(UsageApplicationsAdminModule))]

// Groups the Content Type Usage and Widget Usage applications in the admin application list
[assembly: UICategory(
    codeName: UsageApplicationsAdminModule.CATEGORY,
    name: "Usage reports",
    icon: Icons.Table,
    order: 100)]

namespace XperienceCommunity.UsageApplications
{
    internal class UsageApplicationsAdminModule : AdminModule
    {
        public const string MODULE_NAME = "XperienceCommunity.UsageApplications";
        public const string CATEGORY = "XperienceCommunity.UsageApplications.Category";

        public UsageApplicationsAdminModule()
            : base(MODULE_NAME)
        {
        }
    }
}
