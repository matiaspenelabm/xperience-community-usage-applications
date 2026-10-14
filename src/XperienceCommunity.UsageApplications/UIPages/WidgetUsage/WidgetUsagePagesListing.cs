using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMS.Base;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Websites.UIPages;
using XperienceCommunity.UsageApplications.Features.WidgetUsage;
using XperienceCommunity.UsageApplications.UIPages.WidgetUsage;

[assembly: UIPage(
    parentType: typeof(WidgetUsageListing),
    slug: PageParameterConstants.PARAMETERIZED_SLUG,
    uiPageType: typeof(WidgetUsagePagesListing),
    name: "Pages using widget",
    templateName: TemplateNames.LISTING,
    order: UIPageOrder.First)]

namespace XperienceCommunity.UsageApplications.UIPages.WidgetUsage
{
    /// <summary>
    /// Lists every page (per language) that uses the widget passed in the URL. Each row can open the
    /// page's Page Builder directly.
    /// </summary>
    public class WidgetUsagePagesListing : DataContainerListingPage
    {
        private const string ColumnPageName = "PageName";
        private const string ColumnTreePath = "TreePath";
        private const string ColumnLanguage = "Language";
        private const string ColumnChannel = "Channel";
        // Hidden column carrying the unique row identifier (page + language).
        private const string ColumnRowId = "RowId";
        // Hidden column carrying the resolved Page Builder path; empty when the page cannot be linked.
        private const string ColumnPageBuilderPath = "PageBuilderPath";

        private readonly IWidgetUsageService _widgetUsageService;
        private readonly IPageLinkGenerator _pageLinkGenerator;

        public WidgetUsagePagesListing(IWidgetUsageService widgetUsageService, IPageLinkGenerator pageLinkGenerator)
        {
            _widgetUsageService = widgetUsageService;
            _pageLinkGenerator = pageLinkGenerator;
        }

        /// <summary>
        /// The widget identifier being inspected, taken from the URL.
        /// </summary>
        [PageParameter(typeof(StringPageModelBinder))]
        public string WidgetIdentifier { get; set; }

        public override Task ConfigurePage()
        {
            PageConfiguration.ColumnConfigurations
                .AddColumn(ColumnPageName, "Page", searchable: true)
                .AddColumn(ColumnTreePath, "Path", searchable: true)
                .AddColumn(ColumnLanguage, "Language")
                .AddColumn(ColumnChannel, "Channel")
                .AddColumn(ColumnRowId, "Row ID", visible: false)
                .AddColumn(ColumnPageBuilderPath, "Page Builder path", visible: false);

            PageConfiguration.Caption = $"Pages using {WidgetIdentifier}";

            // Hidden when no Page Builder path could be resolved.
            PageConfiguration.TableActions.AddCommand(new AddCommandParameters(
                commandName: nameof(OpenInPageBuilder),
                label: "Open in Page Builder")
            {
                Icon = Icons.ArrowRightTopSquare,
                ActionStateEvaluator = (action, dataContainer, cancellationToken) =>
                {
                    action.Visible = !string.IsNullOrEmpty(dataContainer.GetValue(ColumnPageBuilderPath)?.ToString());
                    return Task.CompletedTask;
                }
            });

            return base.ConfigurePage();
        }

        /// <summary>
        /// Navigates to the Page Builder of the clicked page. The row identifier is the resolved Page Builder path.
        /// </summary>
        [PageCommand]
        public async Task<INavigateResponse> OpenInPageBuilder(string id)
        {
            // Command parameters are deserialized from the client payload, so a CancellationToken cannot be declared here.
            // The row id is "{webPageItemId}_{language}"; load only that page rather than every page using the widget.
            var separator = id.IndexOf('_');
            var pages = separator > 0 && int.TryParse(id[..separator], out var webPageItemId)
                ? await _widgetUsageService.GetPagesUsingWidgetAsync(WidgetIdentifier, webPageItemId)
                : Array.Empty<WidgetPageUsage>();
            var page = pages.FirstOrDefault(candidate => GetRowId(candidate) == id);
            var path = page is null ? string.Empty : GetPageBuilderPath(page);

            // The row action is hidden for unlinkable pages, so an empty path only occurs if the page vanished.
            return NavigateTo(string.IsNullOrEmpty(path)
                ? _pageLinkGenerator.GetPath(typeof(WidgetUsageListing))
                : path);
        }

        protected override async Task<IEnumerable<IDataContainer>> LoadDataContainers(CancellationToken cancellationToken)
        {
            var pages = await _widgetUsageService.GetPagesUsingWidgetAsync(WidgetIdentifier, cancellationToken: cancellationToken);

            return pages.Select(ToDataContainer).ToList();
        }

        protected override string GetIdentifier(IDataContainer dataContainer)
        {
            return dataContainer.GetValue(ColumnRowId)?.ToString() ?? string.Empty;
        }

        // A page appears once per language, so the id combines both.
        private static string GetRowId(WidgetPageUsage page) => $"{page.WebPageItemId}_{page.LanguageName}";

        private IDataContainer ToDataContainer(WidgetPageUsage page)
        {
            var container = new DataContainer();
            container.SetValue(ColumnPageName, page.PageName);
            container.SetValue(ColumnTreePath, page.TreePath);
            container.SetValue(ColumnLanguage, page.LanguageDisplayName);
            container.SetValue(ColumnChannel, page.ChannelName);
            container.SetValue(ColumnRowId, GetRowId(page));
            container.SetValue(ColumnPageBuilderPath, GetPageBuilderPath(page));
            return container;
        }

        /// <summary>
        /// Builds the admin Page Builder path for a page using the strongly-typed web page route identifiers,
        /// e.g. /webpages-{channelId}/{language}_{webPageItemId}/page-builder.
        /// </summary>
        private string GetPageBuilderPath(WidgetPageUsage page)
        {
            if (string.IsNullOrEmpty(page.LanguageName))
            {
                return string.Empty;
            }

            // PageParameterValues is keyed by the page type that owns each parameterized slug
            // (WebPagesApplication -> channel, WebPageLayout -> language + web page item).
            // GetPath serializes each value via ToString(), so we pass the raw slug segments directly:
            // WebPagesApplicationUrlIdentifier does not override ToString(), which would otherwise emit the type name.
            return _pageLinkGenerator.GetPath(
                typeof(PageBuilderTab),
                new PageParameterValues
                {
                    { typeof(WebPagesApplication), $"webpages-{page.WebsiteChannelId}" },
                    { typeof(WebPageLayout), $"{page.LanguageName}_{page.WebPageItemId}" },
                });
        }
    }
}
