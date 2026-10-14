#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.DataEngine;
using CMS.DataEngine.Query;
using CMS.Websites.Internal;

namespace XperienceCommunity.UsageApplications.Features.ContentTypeUsage
{
    /// <summary>
    /// Default implementation of <see cref="IContentTypeUsageService"/>.
    /// Enumerates content types from the data class metadata and counts their content items.
    /// </summary>
    public class ContentTypeUsageService : IContentTypeUsageService
    {
        private const string KindWebsite = "Website";
        private const string KindReusable = "Reusable";

        // Keeps each IN (...) clause small so very large content types stay within SQL Server limits.
        private const int IdChunkSize = 1000;

        public async Task<IReadOnlyList<ContentTypeUsageItem>> GetContentTypeUsagesAsync(CancellationToken cancellationToken = default)
        {
            var contentTypes = await GetContentTypesAsync(cancellationToken);
            var counts = await CountContentItemsByTypeAsync(cancellationToken);

            return contentTypes
                .Select(contentType => ToUsageItem(contentType, counts))
                .OrderByDescending(usage => usage.ItemCount)
                .ThenBy(usage => usage.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<ContentTypeUsageItem?> GetContentTypeAsync(int contentTypeId, CancellationToken cancellationToken = default)
        {
            var contentType = (await DataClassInfoProvider.GetClasses()
                .WhereEquals(nameof(DataClassInfo.ClassID), contentTypeId)
                .Columns(
                    nameof(DataClassInfo.ClassID),
                    nameof(DataClassInfo.ClassName),
                    nameof(DataClassInfo.ClassDisplayName),
                    nameof(DataClassInfo.ClassContentTypeType))
                .TopN(1)
                .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                .FirstOrDefault();

            if (contentType is null)
            {
                return null;
            }

            var count = await ContentItemInfo.Provider.Get()
                .WhereEquals(nameof(ContentItemInfo.ContentItemContentTypeID), contentTypeId)
                .GetCountAsync(cancellationToken);

            return ToUsageItem(contentType, new Dictionary<int, int> { [contentTypeId] = count });
        }

        public async Task<ContentTypeContentItem?> GetContentItemAsync(int contentTypeId, int contentItemId, CancellationToken cancellationToken = default)
        {
            return (await GetContentItemsAsync(contentTypeId, new[] { contentItemId }, cancellationToken)).FirstOrDefault();
        }

        public async Task<IReadOnlyList<ContentTypeContentItem>> GetContentItemsAsync(int contentTypeId, IReadOnlyCollection<int> contentItemIds, CancellationToken cancellationToken = default)
        {
            if (contentItemIds.Count == 0)
            {
                return Array.Empty<ContentTypeContentItem>();
            }

            var query = ContentItemInfo.Provider.Get()
                .WhereEquals(nameof(ContentItemInfo.ContentItemContentTypeID), contentTypeId)
                .WhereIn(nameof(ContentItemInfo.ContentItemID), contentItemIds);

            var items = (await query
                .Columns(
                    nameof(ContentItemInfo.ContentItemID),
                    nameof(ContentItemInfo.ContentItemGUID),
                    nameof(ContentItemInfo.ContentItemName),
                    nameof(ContentItemInfo.ContentItemIsReusable),
                    nameof(ContentItemInfo.ContentItemWorkspaceID))
                .OrderBy(nameof(ContentItemInfo.ContentItemName))
                .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                .ToList();

            if (items.Count == 0)
            {
                return Array.Empty<ContentTypeContentItem>();
            }

            var ids = items.Select(item => item.ContentItemID).ToList();
            var languageByItem = await GetItemLanguagesAsync(ids, cancellationToken);
            var webPageByItem = await GetWebPagesAsync(ids, cancellationToken);

            return items
                .Select(item =>
                {
                    languageByItem.TryGetValue(item.ContentItemID, out var language);
                    webPageByItem.TryGetValue(item.ContentItemID, out var webPage);

                    return new ContentTypeContentItem(
                        item.ContentItemID,
                        item.ContentItemGUID,
                        item.ContentItemName,
                        string.IsNullOrWhiteSpace(language.DisplayName) ? item.ContentItemName : language.DisplayName,
                        item.ContentItemIsReusable,
                        item.ContentItemWorkspaceID,
                        language.LanguageName ?? string.Empty,
                        webPage?.WebPageItemID ?? 0,
                        webPage?.WebPageItemWebsiteChannelID ?? 0);
                })
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Reads every content type of a supported kind (reusable content or website pages). Other classes
        /// (system tables, forms, module classes) and other content kinds have no editor link to open and are skipped.
        /// </summary>
        private async Task<IReadOnlyList<DataClassInfo>> GetContentTypesAsync(CancellationToken cancellationToken)
        {
            return (await DataClassInfoProvider.GetClasses()
                .WhereIn(nameof(DataClassInfo.ClassContentTypeType), new[] { KindWebsite, KindReusable })
                .Columns(
                    nameof(DataClassInfo.ClassID),
                    nameof(DataClassInfo.ClassName),
                    nameof(DataClassInfo.ClassDisplayName),
                    nameof(DataClassInfo.ClassContentTypeType))
                .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                .ToList();
        }

        /// <summary>
        /// Tallies the number of content items per content type. A content item is language-independent, so a
        /// single row is counted regardless of how many language variants it has.
        /// </summary>
        private async Task<Dictionary<int, int>> CountContentItemsByTypeAsync(CancellationToken cancellationToken)
        {
            var counts = new Dictionary<int, int>();

            var query = $"""
                SELECT [{nameof(ContentItemInfo.ContentItemContentTypeID)}], COUNT(*)
                FROM [CMS_ContentItem]
                GROUP BY [{nameof(ContentItemInfo.ContentItemContentTypeID)}]
                """;
            using var reader = await ConnectionHelper.ExecuteReaderAsync(
                query,
                null,
                QueryTypeEnum.SQLQuery,
                CommandBehavior.Default,
                cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                // Orphaned content items can have a null content type; they belong to no listed type.
                if (!reader.IsDBNull(0))
                {
                    counts[reader.GetInt32(0)] = reader.GetInt32(1);
                }
            }

            return counts;
        }

        /// <summary>
        /// Returns the display name and a language variant to open for each content item. The default language
        /// is preferred; when the item has no variant in the default language the first available variant is used.
        /// </summary>
        private async Task<Dictionary<int, (string DisplayName, string LanguageName)>> GetItemLanguagesAsync(IReadOnlyCollection<int> contentItemIds, CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, (string DisplayName, string LanguageName)>();
            if (contentItemIds.Count == 0)
            {
                return result;
            }

            var languages = (await ContentLanguageInfo.Provider.Get()
                .Columns(
                    nameof(ContentLanguageInfo.ContentLanguageID),
                    nameof(ContentLanguageInfo.ContentLanguageName),
                    nameof(ContentLanguageInfo.ContentLanguageIsDefault))
                .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken))
                .ToList();

            var languageNameById = languages.ToDictionary(language => language.ContentLanguageID, language => language.ContentLanguageName);
            var defaultLanguage = languages.FirstOrDefault(language => language.ContentLanguageIsDefault) ?? languages.FirstOrDefault();

            var metadata = new List<ContentItemLanguageMetadataInfo>();
            foreach (var idChunk in contentItemIds.Chunk(IdChunkSize))
            {
                metadata.AddRange(await ContentItemLanguageMetadataInfo.Provider.Get()
                    .WhereIn(nameof(ContentItemLanguageMetadataInfo.ContentItemLanguageMetadataContentItemID), idChunk)
                    .Columns(
                        nameof(ContentItemLanguageMetadataInfo.ContentItemLanguageMetadataContentItemID),
                        nameof(ContentItemLanguageMetadataInfo.ContentItemLanguageMetadataContentLanguageID),
                        nameof(ContentItemLanguageMetadataInfo.ContentItemLanguageMetadataDisplayName))
                    .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken));
            }

            foreach (var group in metadata.GroupBy(item => item.ContentItemLanguageMetadataContentItemID))
            {
                var variants = group.ToList();

                // Prefer the default language variant, otherwise fall back to the first available variant.
                var preferred = variants.FirstOrDefault(variant => defaultLanguage != null
                        && variant.ContentItemLanguageMetadataContentLanguageID == defaultLanguage.ContentLanguageID)
                    ?? variants.First();

                var displayName = string.IsNullOrWhiteSpace(preferred.ContentItemLanguageMetadataDisplayName)
                    ? variants.Select(variant => variant.ContentItemLanguageMetadataDisplayName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty
                    : preferred.ContentItemLanguageMetadataDisplayName;

                languageNameById.TryGetValue(preferred.ContentItemLanguageMetadataContentLanguageID, out var languageName);

                result[group.Key] = (displayName, languageName ?? defaultLanguage?.ContentLanguageName ?? string.Empty);
            }

            return result;
        }

        /// <summary>
        /// Returns the web page item (if any) backing each content item, so website pages can be linked to the
        /// page editor. Reusable content items have no web page and are absent from the result.
        /// </summary>
        private async Task<Dictionary<int, WebPageItemInfo>> GetWebPagesAsync(IReadOnlyCollection<int> contentItemIds, CancellationToken cancellationToken)
        {
            if (contentItemIds.Count == 0)
            {
                return new Dictionary<int, WebPageItemInfo>();
            }

            var webPages = new List<WebPageItemInfo>();
            foreach (var idChunk in contentItemIds.Chunk(IdChunkSize))
            {
                webPages.AddRange(await WebPageItemInfo.Provider.Get()
                    .WhereIn(nameof(WebPageItemInfo.WebPageItemContentItemID), idChunk)
                    .Columns(
                        nameof(WebPageItemInfo.WebPageItemContentItemID),
                        nameof(WebPageItemInfo.WebPageItemID),
                        nameof(WebPageItemInfo.WebPageItemWebsiteChannelID))
                    .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken));
            }

            return webPages
                .GroupBy(webPage => webPage.WebPageItemContentItemID)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static ContentTypeUsageItem ToUsageItem(DataClassInfo contentType, IReadOnlyDictionary<int, int> counts)
        {
            counts.TryGetValue(contentType.ClassID, out var count);

            return new ContentTypeUsageItem(
                contentType.ClassID,
                contentType.ClassName,
                string.IsNullOrWhiteSpace(contentType.ClassDisplayName) ? contentType.ClassName : contentType.ClassDisplayName,
                MapKind(contentType.ClassContentTypeType),
                count);
        }

        private static string MapKind(string? classContentTypeType)
        {
            return classContentTypeType switch
            {
                KindWebsite => "Page",
                KindReusable => "Reusable content",
                _ => string.IsNullOrWhiteSpace(classContentTypeType) ? "Content" : classContentTypeType
            };
        }
    }
}
