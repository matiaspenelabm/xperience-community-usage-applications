using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XperienceCommunity.UsageApplications.Features.WidgetUsage
{
    /// <summary>
    /// Calculates how many times each page builder widget is used across the site content.
    /// </summary>
    public interface IWidgetUsageService
    {
        /// <summary>
        /// Returns every registered widget together with the number of times it is placed in the latest
        /// version of the site content, ordered by usage (descending). Widgets that are no longer
        /// registered but still present in content are also included and flagged.
        /// </summary>
        Task<IReadOnlyList<WidgetUsageItem>> GetWidgetUsagesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every page (web page item + language variant) whose latest version contains the given widget.
        /// </summary>
        /// <param name="widgetIdentifier">The registered widget identifier (the "type" stored in the page builder configuration).</param>
        /// <param name="webPageItemId">When set, restricts the result to the page with this web page item ID.</param>
        Task<IReadOnlyList<WidgetPageUsage>> GetPagesUsingWidgetAsync(string widgetIdentifier, int? webPageItemId = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Usage information for a single widget type.
    /// </summary>
    /// <param name="Identifier">The registered widget identifier stored in the page builder configuration (e.g. "HeroWidgetController").</param>
    /// <param name="DisplayName">The friendly widget name shown in the page builder (e.g. "Hero"). Falls back to the identifier for unregistered widgets.</param>
    /// <param name="UsageCount">The number of placements found across the latest content versions.</param>
    /// <param name="IsRegistered">Whether the widget is still registered in code. False indicates a legacy/orphaned widget kept only in content.</param>
    public record WidgetUsageItem(string Identifier, string DisplayName, int UsageCount, bool IsRegistered);

    /// <summary>
    /// A single page (in a specific language) that uses a widget, with the data needed to deep-link into its Page Builder.
    /// </summary>
    /// <param name="WebPageItemId">The web page item ID (used in the Page Builder admin URL).</param>
    /// <param name="WebsiteChannelId">The website channel ID (used in the Page Builder admin URL).</param>
    /// <param name="LanguageName">The content language code, e.g. "en" (used in the Page Builder admin URL).</param>
    /// <param name="LanguageDisplayName">The friendly language name shown in the listing.</param>
    /// <param name="PageName">The web page item name.</param>
    /// <param name="TreePath">The content-tree path of the page.</param>
    /// <param name="ChannelName">A friendly channel identifier (domain) shown in the listing.</param>
    public record WidgetPageUsage(
        int WebPageItemId,
        int WebsiteChannelId,
        string LanguageName,
        string LanguageDisplayName,
        string PageName,
        string TreePath,
        string ChannelName);
}
