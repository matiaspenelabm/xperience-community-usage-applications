#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XperienceCommunity.UsageApplications.Features.ContentTypeUsage
{
    /// <summary>
    /// Reports how many content items exist for each content type and lists those items.
    /// </summary>
    public interface IContentTypeUsageService
    {
        /// <summary>
        /// Returns every content type (both reusable content and page types) together with the number of
        /// content items stored for it, ordered by usage (descending).
        /// </summary>
        Task<IReadOnlyList<ContentTypeUsageItem>> GetContentTypeUsagesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the content type identified by <paramref name="contentTypeId"/>, or <c>null</c> when it no
        /// longer exists.
        /// </summary>
        Task<ContentTypeUsageItem?> GetContentTypeAsync(int contentTypeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the specified content items that use the content type identified by <paramref name="contentTypeId"/>.
        /// </summary>
        Task<IReadOnlyList<ContentTypeContentItem>> GetContentItemsAsync(int contentTypeId, IReadOnlyCollection<int> contentItemIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a single content item of the given content type, or <c>null</c> when it no longer exists.
        /// </summary>
        Task<ContentTypeContentItem?> GetContentItemAsync(int contentTypeId, int contentItemId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Usage information for a single content type.
    /// </summary>
    /// <param name="ContentTypeId">The content type (data class) identifier.</param>
    /// <param name="CodeName">The content type code name (e.g. "Acme.Hero").</param>
    /// <param name="DisplayName">The friendly content type name shown in the admin UI. Falls back to the code name.</param>
    /// <param name="Kind">Human readable content type kind ("Page" or "Reusable content").</param>
    /// <param name="ItemCount">The number of content items stored for this content type.</param>
    public record ContentTypeUsageItem(int ContentTypeId, string CodeName, string DisplayName, string Kind, int ItemCount);

    /// <summary>
    /// A single content item that uses a given content type, together with the data needed to deep link
    /// to the item's editing UI.
    /// </summary>
    /// <param name="ContentItemId">The content item identifier.</param>
    /// <param name="ContentItemGuid">The content item GUID.</param>
    /// <param name="CodeName">The content item code name.</param>
    /// <param name="DisplayName">The content item display name. Falls back to the code name.</param>
    /// <param name="IsReusable">True for reusable content (edited in the Content hub); false for website pages.</param>
    /// <param name="WorkspaceId">The content hub workspace the item belongs to (reusable content only).</param>
    /// <param name="LanguageName">The code name of a language the item has a variant in, used to open the editor.</param>
    /// <param name="WebPageItemId">The web page item identifier (website pages only; 0 otherwise).</param>
    /// <param name="WebsiteChannelId">The website channel identifier (website pages only; 0 otherwise).</param>
    public record ContentTypeContentItem(
        int ContentItemId,
        Guid ContentItemGuid,
        string CodeName,
        string DisplayName,
        bool IsReusable,
        int WorkspaceId,
        string LanguageName,
        int WebPageItemId,
        int WebsiteChannelId);
}
