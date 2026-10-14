using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMS.Base;
using Kentico.Xperience.Admin.Base;
using XperienceCommunity.UsageApplications.Features.WidgetUsage;
using XperienceCommunity.UsageApplications.UIPages.WidgetUsage;
using XperienceCommunity.UsageApplications.UIPages;

[assembly: UIPage(
    parentType: typeof(WidgetUsageApplication),
    slug: "list",
    uiPageType: typeof(WidgetUsageListing),
    name: "All widgets",
    templateName: TemplateNames.LISTING,
    order: UIPageOrder.First)]

namespace XperienceCommunity.UsageApplications.UIPages.WidgetUsage
{
    /// <summary>
    /// Read-only listing that shows each widget and how many times it is used across the site content.
    /// Selecting a row opens a listing of the pages that use the widget.
    /// </summary>
    public class WidgetUsageListing : DataContainerListingPage
    {
        private const string ColumnDisplayName = "DisplayName";
        private const string ColumnIdentifier = "Identifier";
        private const string ColumnUsageCount = "UsageCount";
        private const string ColumnStatus = "Status";

        private readonly IWidgetUsageService _widgetUsageService;

        public WidgetUsageListing(IWidgetUsageService widgetUsageService)
        {
            _widgetUsageService = widgetUsageService;
        }

        public override Task ConfigurePage()
        {
            PageConfiguration.ColumnConfigurations
                .AddColumn(ColumnDisplayName, "Widget", searchable: true)
                .AddColumn(ColumnIdentifier, "Identifier", searchable: true)
                .AddColumn(ColumnUsageCount, "Times used")
                .AddColumn(ColumnStatus, "Status");

            PageConfiguration.Caption = "Widget Usage";

            // The whole row is clickable, matching the Content Type Usage listing. Unused widgets have no pages to show.
            PageConfiguration.AddEditRowAction<WidgetUsagePagesListing>(
                (action, dataContainer) => action.Visible = GetUsageCount(dataContainer) > 0);

            return base.ConfigurePage();
        }

        protected override async Task<IEnumerable<IDataContainer>> LoadDataContainers(CancellationToken cancellationToken)
        {
            var usages = await _widgetUsageService.GetWidgetUsagesAsync(cancellationToken);

            return usages.Select(ToDataContainer).ToList();
        }

        protected override string GetIdentifier(IDataContainer dataContainer)
        {
            return dataContainer.GetValue(ColumnIdentifier)?.ToString() ?? string.Empty;
        }

        private static int GetUsageCount(IDataContainer dataContainer)
        {
            var value = dataContainer.GetValue(ColumnUsageCount);
            return value is null ? 0 : Convert.ToInt32(value);
        }

        private static IDataContainer ToDataContainer(WidgetUsageItem usage)
        {
            var container = new DataContainer();
            container.SetValue(ColumnDisplayName, usage.DisplayName);
            container.SetValue(ColumnIdentifier, usage.Identifier);
            container.SetValue(ColumnUsageCount, usage.UsageCount);
            container.SetValue(ColumnStatus, usage.IsRegistered ? "Registered" : "Legacy / not registered");
            return container;
        }
    }
}
