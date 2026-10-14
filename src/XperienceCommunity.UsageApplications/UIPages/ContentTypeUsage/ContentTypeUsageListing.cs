using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMS.Base;
using Kentico.Xperience.Admin.Base;
using XperienceCommunity.UsageApplications.Features.ContentTypeUsage;
using XperienceCommunity.UsageApplications.UIPages.ContentTypeUsage;
using XperienceCommunity.UsageApplications.UIPages;

[assembly: UIPage(
    parentType: typeof(ContentTypeUsageApplication),
    slug: "list",
    uiPageType: typeof(ContentTypeUsageListing),
    name: "All content types",
    templateName: TemplateNames.LISTING,
    order: UIPageOrder.First)]

namespace XperienceCommunity.UsageApplications.UIPages.ContentTypeUsage
{
    /// <summary>
    /// Read-only listing that shows each content type and how many content items use it. Selecting a row
    /// opens a listing of the content items for that content type.
    /// </summary>
    public class ContentTypeUsageListing : DataContainerListingPage
    {
        private const string ColumnContentTypeId = "ContentTypeId";
        private const string ColumnDisplayName = "DisplayName";
        private const string ColumnCodeName = "CodeName";
        private const string ColumnKind = "Kind";
        private const string ColumnItemCount = "ItemCount";

        private readonly IContentTypeUsageService _contentTypeUsageService;

        public ContentTypeUsageListing(IContentTypeUsageService contentTypeUsageService)
        {
            _contentTypeUsageService = contentTypeUsageService;
        }

        public override Task ConfigurePage()
        {
            PageConfiguration.ColumnConfigurations
                .AddColumn(ColumnDisplayName, "Content type", searchable: true)
                .AddColumn(ColumnCodeName, "Code name", searchable: true)
                .AddColumn(ColumnKind, "Type")
                .AddColumn(ColumnItemCount, "Items");

            PageConfiguration.AddEditRowAction<ContentTypeItemsListing>();

            PageConfiguration.Caption = "Content Type Usage";

            return base.ConfigurePage();
        }

        protected override async Task<IEnumerable<IDataContainer>> LoadDataContainers(CancellationToken cancellationToken)
        {
            var usages = await _contentTypeUsageService.GetContentTypeUsagesAsync(cancellationToken);

            return usages.Select(ToDataContainer).ToList();
        }

        protected override string GetIdentifier(IDataContainer dataContainer)
        {
            return dataContainer.GetValue(ColumnContentTypeId)?.ToString() ?? string.Empty;
        }

        private static IDataContainer ToDataContainer(ContentTypeUsageItem usage)
        {
            var container = new DataContainer();
            container.SetValue(ColumnContentTypeId, usage.ContentTypeId);
            container.SetValue(ColumnDisplayName, usage.DisplayName);
            container.SetValue(ColumnCodeName, usage.CodeName);
            container.SetValue(ColumnKind, usage.Kind);
            container.SetValue(ColumnItemCount, usage.ItemCount);
            return container;
        }
    }
}
