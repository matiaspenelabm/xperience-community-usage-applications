#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CMS.Base;
using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.Core;
using CMS.DataEngine;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Admin.Websites.UIPages;
using XperienceCommunity.UsageApplications.Features.ContentTypeUsage;
using XperienceCommunity.UsageApplications.UIPages.ContentTypeUsage;

[assembly: UIPage(
    parentType: typeof(ContentTypeUsageListing),
    slug: PageParameterConstants.PARAMETERIZED_SLUG,
    uiPageType: typeof(ContentTypeItemsListing),
    name: "Content Items",
    templateName: TemplateNames.LISTING,
    order: UIPageOrder.First)]

namespace XperienceCommunity.UsageApplications.UIPages.ContentTypeUsage
{
    /// <summary>
    /// Read-only listing of every content item that uses the content type selected on
    /// <see cref="ContentTypeUsageListing"/>. Each row can open the content item in its editor.
    /// </summary>
    public class ContentTypeItemsListing : ListingPage
    {
        private const string EventSource = "ContentTypeUsage";

        // Derived column: the item's display name from the language metadata, filled in by LoadListingDataContainers.
        private const string ColumnDisplayName = "DisplayName";

        // The "All content items" folder slug used by the Content hub routing.
        private const string ContentHubAllFolder = "all";

        // ContentHubLinkParametersHelper is internal in the Kentico admin package, so its parameter builder is
        // invoked via reflection. The host application may run a newer Kentico version than this package was built
        // against, so a missing method is logged in ConfigurePage and reusable items are left without an editor link.
        private static readonly MethodInfo? ContentHubGetParameters = typeof(ContentItemEdit).Assembly
            .GetType("Kentico.Xperience.Admin.Base.ContentHubLinkParametersHelper")?
            .GetMethod(
                "GetParameters",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(int), typeof(string), typeof(string), typeof(int) },
                modifiers: null);

        private readonly IContentTypeUsageService _contentTypeUsageService;
        private readonly IPageLinkGenerator _pageLinkGenerator;
        private readonly IEventLogService _eventLogService;

        protected override string ObjectType => ContentItemInfo.OBJECT_TYPE;

        public ContentTypeItemsListing(
            IContentTypeUsageService contentTypeUsageService,
            IPageLinkGenerator pageLinkGenerator,
            IEventLogService eventLogService)
        {
            _contentTypeUsageService = contentTypeUsageService;
            _pageLinkGenerator = pageLinkGenerator;
            _eventLogService = eventLogService;
        }

        /// <summary>
        /// The content type identifier, supplied by the row action on <see cref="ContentTypeUsageListing"/>.
        /// </summary>
        [PageParameter(typeof(IntPageModelBinder))]
        public int ContentTypeId { get; set; }

        public override async Task ConfigurePage()
        {
            if (ContentHubGetParameters is null)
            {
                _eventLogService.LogWarning(
                    EventSource,
                    "CONTENTHUBLINKHELPER",
                    "ContentHubLinkParametersHelper.GetParameters was not found; reusable content items cannot be opened from the usage report. The Kentico admin package may have changed.");
            }

            // The display name lives in the language metadata, not in CMS_ContentItem. It gets its own identifier and is
            // filled in by LoadListingDataContainers, so it is excluded from the database query and cannot collide with
            // the code name.
            PageConfiguration.ColumnConfigurations.Add(new ColumnConfiguration
            {
                Name = ColumnDisplayName,
                Caption = "Name",
                LoadedExternally = true,
                // AddColumn's defaults; left at 0 the client renders the cells with no width, so they look empty.
                MinWidth = 8,
                MaxWidth = 100
            });

            PageConfiguration.ColumnConfigurations
                .AddColumn(nameof(ContentItemInfo.ContentItemName), "Code name", searchable: true)
                .AddColumn(nameof(ContentItemInfo.ContentItemGUID), "GUID", searchable: true);

            PageConfiguration.PageSizes = new[] { 25, 50, 100 };
            PageConfiguration.QueryModifiers.AddModifier((query, _) =>
                query.WhereEquals(nameof(ContentItemInfo.ContentItemContentTypeID), ContentTypeId));

            var contentType = await _contentTypeUsageService.GetContentTypeAsync(ContentTypeId);
            PageConfiguration.Caption = contentType is null
                ? "Content Items"
                : $"{contentType.DisplayName} content items";

            // Per-row action that opens the content item's editor.
            PageConfiguration.TableActions.AddCommand(new AddCommandParameters(
                commandName: nameof(OpenItem),
                label: "Open item")
            {
                Icon = Icons.ArrowRightTopSquare
            });

            await base.ConfigurePage();
        }

        /// <summary>
        /// Navigates to the editor of the clicked content item. The row identifier is the content item id; the
        /// editor path is resolved again here so it does not have to be round-tripped through the row.
        /// </summary>
        [PageCommand]
        public async Task<INavigateResponse> OpenItem(int id)
        {
            // Command parameters are deserialized from the client payload, so a CancellationToken cannot be declared here.
            // The row identifier is numeric, so the client sends it as a JSON number.
            var item = await _contentTypeUsageService.GetContentItemAsync(ContentTypeId, id);
            var path = item is null ? string.Empty : GetEditPath(item);

            // The row action is hidden for unlinkable items, so an empty path only occurs if the item vanished.
            return NavigateTo(string.IsNullOrEmpty(path)
                ? _pageLinkGenerator.GetPath(typeof(ContentTypeUsageListing))
                : path);
        }

        /// <summary>
        /// Loads the current page of items, then fills the externally loaded display name column for those items only.
        /// Kentico builds the table cells (and the export) from these containers, so the value has to be set here.
        /// </summary>
        protected override async Task<ListingData> LoadListingDataContainers(ListingDataSettings settings, CancellationToken cancellationToken)
        {
            var data = await base.LoadListingDataContainers(settings, cancellationToken);

            // The query returns read-only data row containers, so copy each into a container that accepts the extra column.
            var containers = data.DataContainers
                .Select(container =>
                {
                    var copy = new DataContainer();
                    foreach (var columnName in container.ColumnNames)
                    {
                        copy.SetValue(columnName, container.GetValue(columnName));
                    }

                    return copy;
                })
                .ToList();

            var itemIds = containers
                .Select(container => Convert.ToInt32(container.GetValue(nameof(ContentItemInfo.ContentItemID))))
                .ToList();
            var items = await _contentTypeUsageService.GetContentItemsAsync(ContentTypeId, itemIds, cancellationToken);
            var displayNameById = items.ToDictionary(item => item.ContentItemId, item => item.DisplayName);

            foreach (var container in containers)
            {
                var itemId = Convert.ToInt32(container.GetValue(nameof(ContentItemInfo.ContentItemID)));
                container.SetValue(
                    ColumnDisplayName,
                    displayNameById.TryGetValue(itemId, out var displayName)
                        ? displayName
                        : container.GetValue(nameof(ContentItemInfo.ContentItemName)));
            }

            return new ListingData
            {
                DataContainers = containers,
                TotalCount = data.TotalCount
            };
        }

        /// <summary>
        /// Builds the admin editor path for a content item. Reusable content opens in the Content hub; website
        /// items open in the Content tab. Returns an empty string when the item cannot be linked.
        /// </summary>
        private string GetEditPath(ContentTypeContentItem item)
        {
            if (string.IsNullOrEmpty(item.LanguageName))
            {
                return string.Empty;
            }

            if (item.IsReusable)
            {
                try
                {
                    if (ContentHubGetParameters?.Invoke(
                            null,
                            new object[] { item.WorkspaceId, item.LanguageName, ContentHubAllFolder, item.ContentItemId })
                        is PageParameterValues parameters)
                    {
                        return _pageLinkGenerator.GetPath(typeof(ContentItemEdit), parameters);
                    }
                }
                catch (TargetInvocationException exception)
                {
                    _eventLogService.LogException(EventSource, "CONTENTHUBLINK", exception.InnerException ?? exception);
                }

                return string.Empty;
            }

            if (item.WebPageItemId == 0 || item.WebsiteChannelId == 0)
            {
                return string.Empty;
            }

            // Generic website items do not necessarily have a Page Builder tab.
            return _pageLinkGenerator.GetPath(
                typeof(ContentTab),
                new PageParameterValues
                {
                    { typeof(WebPagesApplication), $"webpages-{item.WebsiteChannelId}" },
                    { typeof(WebPageLayout), $"{item.LanguageName}_{item.WebPageItemId}" },
                });
        }
    }
}
