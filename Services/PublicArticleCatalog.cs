using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MelrandiaManagement.Services;

/// <summary>
/// Cached public projection of enabled categories and published/scheduled-due articles. Drafts,
/// archived records, storage paths and internal IDs never cross this boundary.
/// </summary>
public sealed class PublicArticleCatalog(
    IDbContextFactory<ManagementDbContext> factory,
    IMemoryCache cache,
    ArticleMarkdownRenderer markdownRenderer)
{
    private const string CacheKey = "public-article-snapshot-v1";
    private readonly SemaphoreSlim cacheGate = new(1, 1);

    public async Task<IReadOnlyList<PublicArticleCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        (await GetSnapshotAsync(cancellationToken)).Categories;

    public async Task<IReadOnlyList<PublicArticleSummary>> GetPublishedArticlesAsync(string? categorySlug = null,
        int? take = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<PublicArticleSummary> articles = (await GetSnapshotAsync(cancellationToken)).Articles;
        if (!string.IsNullOrWhiteSpace(categorySlug))
            articles = articles.Where(x => x.CategorySlug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase));
        if (take.HasValue) articles = articles.Take(take.Value);
        return articles.ToList();
    }

    public async Task<PublicArticleCategory?> FindCategoryAsync(string slug,
        CancellationToken cancellationToken = default) =>
        (await GetSnapshotAsync(cancellationToken)).Categories.FirstOrDefault(
            x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public async Task<PublicArticleDetail?> FindPublishedArticleAsync(string categorySlug, string articleSlug,
        CancellationToken cancellationToken = default) =>
        (await GetSnapshotAsync(cancellationToken)).Details.FirstOrDefault(x =>
            x.CategorySlug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase) &&
            x.Slug.Equals(articleSlug, StringComparison.OrdinalIgnoreCase));

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<PublicArticleSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PublicArticleSnapshot? current) && current is not null) return current;
        await cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(CacheKey, out current) && current is not null) return current;
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var rows = await db.Articles.AsNoTracking()
                .Where(x => x.Category.IsEnabled &&
                    (x.Status == PortalArticleStatuses.Published ||
                     x.Status == PortalArticleStatuses.Scheduled && x.PublishedAtUtc <= now))
                .OrderByDescending(x => x.PublishedAtUtc)
                .Select(x => new PublicArticleRow(x.Category.Slug, x.Category.DisplayName, x.Slug,
                    x.Title, x.Summary, x.ContentMarkdown, x.Author.DisplayName,
                    x.PublishedAtUtc ?? x.UpdatedAtUtc,
                    x.Media.Where(m => m.IsCover).OrderBy(m => m.DisplayOrder)
                        .Select(m => new PublicArticleMedia(m.PublicUrl, m.AltText, m.Caption)).FirstOrDefault(),
                    x.Media.Where(m => !m.IsCover).OrderBy(m => m.DisplayOrder)
                        .Select(m => new PublicArticleMedia(m.PublicUrl, m.AltText, m.Caption)).ToList()))
                .ToListAsync(cancellationToken);

            var counts = rows.GroupBy(x => x.CategorySlug, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
            var categories = await db.ArticleCategories.AsNoTracking().Where(x => x.IsEnabled)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new PublicArticleCategory(x.Slug, x.DisplayName, x.Summary,
                    x.DisplayOrder.ToString("00"), 0))
                .ToListAsync(cancellationToken);
            categories = categories.Select(x => x with
                { PublishedCount = counts.GetValueOrDefault(x.Slug) }).ToList();

            current = new PublicArticleSnapshot(
                categories,
                rows.Select(x => new PublicArticleSummary(x.CategorySlug, x.CategoryName, x.Slug,
                    x.Title, x.Summary, x.AuthorName, x.Cover?.PublicUrl, x.Cover?.AltText,
                    x.PublishedAtUtc)).ToList(),
                rows.Select(x => new PublicArticleDetail(x.CategorySlug, x.CategoryName, x.Slug,
                    x.Title, x.Summary, markdownRenderer.Render(x.ContentMarkdown), x.AuthorName,
                    x.Cover?.PublicUrl, x.Cover?.AltText, x.PublishedAtUtc, x.Gallery)).ToList());
            cache.Set(CacheKey, current, TimeSpan.FromSeconds(30));
            return current;
        }
        finally { cacheGate.Release(); }
    }

    private sealed record PublicArticleRow(string CategorySlug, string CategoryName, string Slug,
        string Title, string Summary, string ContentMarkdown, string AuthorName,
        DateTime PublishedAtUtc, PublicArticleMedia? Cover, IReadOnlyList<PublicArticleMedia> Gallery);
}
