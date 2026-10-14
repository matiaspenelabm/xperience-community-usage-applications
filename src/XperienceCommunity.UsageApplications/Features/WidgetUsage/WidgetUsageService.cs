using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.DataEngine;
using CMS.Websites;
using CMS.Websites.Internal;
using Kentico.PageBuilder.Web.Mvc;

namespace XperienceCommunity.UsageApplications.Features.WidgetUsage
{
    /// <summary>
    /// Default implementation of <see cref="IWidgetUsageService"/>.
    /// Enumerates registered widgets and counts their placements in the stored page builder configuration.
    /// </summary>
    public class WidgetUsageService : IWidgetUsageService
    {
        private const string WidgetPlacementCacheKey = "XperienceCommunity.UsageApplications.WidgetUsage.Placements";
        private const string LanguageCacheKey = "XperienceCommunity.UsageApplications.WidgetUsage.Languages";
        private const string ChannelCacheKey = "XperienceCommunity.UsageApplications.WidgetUsage.Channels";
        private static readonly TimeSpan WidgetPlacementCacheDuration = TimeSpan.FromMinutes(5);

        private readonly IMemoryCache _memoryCache;
        private readonly IInfoProvider<ContentItemCommonDataInfo> _commonDataProvider;
        private readonly IInfoProvider<WebPageItemInfo> _webPageItemProvider;
        private readonly IInfoProvider<ContentLanguageInfo> _contentLanguageProvider;
        private readonly IInfoProvider<WebsiteChannelInfo> _websiteChannelProvider;

        public WidgetUsageService(
            IMemoryCache memoryCache,
            IInfoProvider<ContentItemCommonDataInfo> commonDataProvider,
            IInfoProvider<WebPageItemInfo> webPageItemProvider,
            IInfoProvider<ContentLanguageInfo> contentLanguageProvider,
            IInfoProvider<WebsiteChannelInfo> websiteChannelProvider)
        {
            _memoryCache = memoryCache;
            _commonDataProvider = commonDataProvider;
            _webPageItemProvider = webPageItemProvider;
            _contentLanguageProvider = contentLanguageProvider;
            _websiteChannelProvider = websiteChannelProvider;
        }

        public async Task<IReadOnlyList<WidgetUsageItem>> GetWidgetUsagesAsync(CancellationToken cancellationToken = default)
        {
            // Registered widgets: identifier -> display name.
            var registered = new Kentico.Builder.Web.Mvc.ComponentDefinitionProvider<WidgetDefinition>()
                .GetAll()
                .GroupBy(definition => definition.Identifier, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

            var placementData = await GetWidgetPlacementDataAsync(cancellationToken);

            var usages = new List<WidgetUsageItem>();

            // Every registered widget, including those with zero usage.
            foreach (var (identifier, displayName) in registered)
            {
                placementData.Counts.TryGetValue(identifier, out var count);
                usages.Add(new WidgetUsageItem(identifier, string.IsNullOrWhiteSpace(displayName) ? identifier : displayName, count, IsRegistered: true));
            }

            // Widgets found in content that are no longer registered in code.
            foreach (var (identifier, count) in placementData.Counts)
            {
                if (!registered.ContainsKey(identifier))
                {
                    usages.Add(new WidgetUsageItem(identifier, identifier, count, IsRegistered: false));
                }
            }

            return usages
                .OrderByDescending(usage => usage.UsageCount)
                .ThenBy(usage => usage.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IReadOnlyList<WidgetPageUsage>> GetPagesUsingWidgetAsync(string widgetIdentifier, int? webPageItemId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(widgetIdentifier))
            {
                return Array.Empty<WidgetPageUsage>();
            }

            var placementData = await GetWidgetPlacementDataAsync(cancellationToken);
            if (!placementData.PagesByWidget.TryGetValue(widgetIdentifier, out var matches) || matches.Count == 0)
            {
                return Array.Empty<WidgetPageUsage>();
            }

            if (webPageItemId.HasValue)
            {
                // Resolve a single page so row actions do not load every page that uses the widget.
                var contentItemId = (await _webPageItemProvider.Get()
                        .WhereEquals(nameof(WebPageItemInfo.WebPageItemID), webPageItemId.Value)
                        .Columns(nameof(WebPageItemInfo.WebPageItemContentItemID))
                        .TopN(1)
                        .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                    .Select(item => (int?)item.WebPageItemContentItemID)
                    .FirstOrDefault();

                if (contentItemId is null)
                {
                    return Array.Empty<WidgetPageUsage>();
                }

                matches = matches.Where(match => match.ContentItemId == contentItemId.Value).ToList();
            }

            var contentItemIds = matches.Select(match => match.ContentItemId).Distinct().ToList();

            // Only content items that are web pages resolve to a WebPageItemInfo (reusable content is skipped).
            const int idChunkSize = 1000;
            var webPageItems = new Dictionary<int, WebPageItemInfo>();
            foreach (var idChunk in contentItemIds.Chunk(idChunkSize))
            {
                var chunkItems = await _webPageItemProvider.Get()
                    .WhereIn(nameof(WebPageItemInfo.WebPageItemContentItemID), idChunk)
                    .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

                foreach (var item in chunkItems)
                {
                    webPageItems[item.WebPageItemContentItemID] = item;
                }
            }

            var languages = await _memoryCache.GetOrCreateAsync(LanguageCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WidgetPlacementCacheDuration;
                return (await _contentLanguageProvider.Get()
                        .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                    .ToDictionary(language => language.ContentLanguageID);
            }) ?? new Dictionary<int, ContentLanguageInfo>();

            var channels = await _memoryCache.GetOrCreateAsync(ChannelCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WidgetPlacementCacheDuration;
                return (await _websiteChannelProvider.Get()
                        .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                    .ToDictionary(channel => channel.WebsiteChannelID);
            }) ?? new Dictionary<int, WebsiteChannelInfo>();

            var usages = new List<WidgetPageUsage>();
            foreach (var (contentItemId, languageId) in matches)
            {
                if (!webPageItems.TryGetValue(contentItemId, out var webPageItem))
                {
                    continue;
                }

                languages.TryGetValue(languageId, out var language);
                channels.TryGetValue(webPageItem.WebPageItemWebsiteChannelID, out var channel);

                var channelName = channel is null
                    ? $"Channel {webPageItem.WebPageItemWebsiteChannelID}"
                    : (string.IsNullOrWhiteSpace(channel.WebsiteChannelDomain)
                        ? $"Channel {channel.WebsiteChannelID}"
                        : channel.WebsiteChannelDomain);

                usages.Add(new WidgetPageUsage(
                    WebPageItemId: webPageItem.WebPageItemID,
                    WebsiteChannelId: webPageItem.WebPageItemWebsiteChannelID,
                    LanguageName: language?.ContentLanguageName ?? string.Empty,
                    LanguageDisplayName: language?.ContentLanguageDisplayName ?? language?.ContentLanguageName ?? string.Empty,
                    PageName: webPageItem.WebPageItemName,
                    TreePath: webPageItem.WebPageItemTreePath,
                    ChannelName: channelName));
            }

            return usages
                .OrderBy(usage => usage.ChannelName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.TreePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(usage => usage.LanguageName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Loads or reuses the cached widget counts and page mapping from latest content configurations.
        /// </summary>
        private async Task<WidgetPlacementData> GetWidgetPlacementDataAsync(CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(WidgetPlacementCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WidgetPlacementCacheDuration;

                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var pagesByWidget = new Dictionary<string, List<(int ContentItemId, int LanguageId)>>(StringComparer.OrdinalIgnoreCase);
                var configurations = await _commonDataProvider.Get()
                    .Columns(
                        nameof(ContentItemCommonDataInfo.ContentItemCommonDataContentItemID),
                        nameof(ContentItemCommonDataInfo.ContentItemCommonDataContentLanguageID),
                        nameof(ContentItemCommonDataInfo.ContentItemCommonDataVisualBuilderWidgets))
                    .WhereEquals(nameof(ContentItemCommonDataInfo.ContentItemCommonDataIsLatest), true)
                    .WhereNotEmpty(nameof(ContentItemCommonDataInfo.ContentItemCommonDataVisualBuilderWidgets))
                    .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

                foreach (var configuration in configurations)
                {
                    var json = configuration.ContentItemCommonDataVisualBuilderWidgets;
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    try
                    {
                        using var document = JsonDocument.Parse(json);
                        var configurationCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        CollectWidgetTypes(document.RootElement, configurationCounts);

                        foreach (var (identifier, count) in configurationCounts)
                        {
                            counts[identifier] = counts.TryGetValue(identifier, out var current) ? current + count : count;

                            if (!pagesByWidget.TryGetValue(identifier, out var pages))
                            {
                                pages = new List<(int ContentItemId, int LanguageId)>();
                                pagesByWidget[identifier] = pages;
                            }

                            pages.Add((
                                configuration.ContentItemCommonDataContentItemID,
                                configuration.ContentItemCommonDataContentLanguageID));
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed configuration rather than failing the whole report.
                    }
                }

                return new WidgetPlacementData(counts, pagesByWidget);
            }) ?? new WidgetPlacementData(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, List<(int ContentItemId, int LanguageId)>>(StringComparer.OrdinalIgnoreCase));
        }

        private sealed record WidgetPlacementData(
            Dictionary<string, int> Counts,
            Dictionary<string, List<(int ContentItemId, int LanguageId)>> PagesByWidget);

        /// <summary>
        /// Walks the page builder JSON and increments the count for each widget's "type" identifier.
        /// The configuration nests as editableAreas -> sections -> zones -> widgets, so we recurse and
        /// tally the "type" of every element that appears inside a "widgets" array.
        /// </summary>
        private static void CollectWidgetTypes(JsonElement element, Dictionary<string, int> counts)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.NameEquals("widgets") && property.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var widget in property.Value.EnumerateArray())
                            {
                                if (widget.ValueKind == JsonValueKind.Object
                                    && widget.TryGetProperty("type", out var type)
                                    && type.ValueKind == JsonValueKind.String)
                                {
                                    var identifier = type.GetString();
                                    if (!string.IsNullOrWhiteSpace(identifier))
                                    {
                                        counts[identifier] = counts.TryGetValue(identifier, out var current) ? current + 1 : 1;
                                    }
                                }

                                // Recurse to support any widgets nested inside a widget's own editable areas.
                                CollectWidgetTypes(widget, counts);
                            }
                        }
                        else
                        {
                            CollectWidgetTypes(property.Value, counts);
                        }
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CollectWidgetTypes(item, counts);
                    }

                    break;
            }
        }
    }
}
