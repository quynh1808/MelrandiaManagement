namespace MelrandiaManagement.Models;

/// <summary>Stable workflow values persisted with every article.</summary>
public static class PortalArticleStatuses
{
    public const string Draft = "Draft";
    public const string Scheduled = "Scheduled";
    public const string Published = "Published";
    public const string Archived = "Archived";
    public static readonly string[] All = [Draft, Scheduled, Published, Archived];
}

/// <summary>Persistent editorial category used by both public navigation and Management.</summary>
public sealed class PortalArticleCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public ICollection<PortalArticle> Articles { get; set; } = [];
}

/// <summary>
/// Authoritative article record. ContentMarkdown remains portable source; rendered HTML is
/// produced on read and never accepted directly from the browser.
/// </summary>
public sealed class PortalArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public Guid AuthorId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public string Status { get; set; } = PortalArticleStatuses.Draft;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public PortalArticleCategory Category { get; set; } = null!;
    public PortalUser Author { get; set; } = null!;
    public ICollection<PortalArticleMedia> Media { get; set; } = [];
}

/// <summary>Searchable metadata for an image whose bytes live in persistent media storage.</summary>
public sealed class PortalArticleMedia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string AltText { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public bool IsCover { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public PortalArticle Article { get; set; } = null!;
}

public sealed record ArticleAdminSummary(Guid Id, string Slug, string Title, string Summary,
    string Status, string CategoryName, string AuthorName, string? CoverUrl,
    DateTime? PublishedAtUtc, DateTime UpdatedAtUtc, int MediaCount, int Version);

public sealed record ArticleAdminSnapshot(IReadOnlyList<PortalArticleCategory> Categories,
    IReadOnlyList<ArticleAdminSummary> Articles);

public sealed record ArticleCountTrend(int TotalCount, int MonthOverMonthChange);

public sealed record ArticleAdminDetail(Guid Id, Guid CategoryId, string Slug, string Title,
    string Summary, string ContentMarkdown, string Status, DateTime? PublishedAtUtc,
    string AuthorName, IReadOnlyList<PortalArticleMedia> Media, int Version);

public sealed record PublicArticleSummary(string CategorySlug, string CategoryName, string Slug,
    string Title, string Summary, string AuthorName, string? CoverUrl, string? CoverAlt,
    DateTime PublishedAtUtc);

public sealed record PublicArticleDetail(string CategorySlug, string CategoryName, string Slug,
    string Title, string Summary, string ContentHtml, string AuthorName, string? CoverUrl,
    string? CoverAlt, DateTime PublishedAtUtc, IReadOnlyList<PublicArticleMedia> Gallery);

public sealed record PublicArticleMedia(string PublicUrl, string AltText, string Caption);

public sealed record PublicArticleSnapshot(IReadOnlyList<PublicArticleCategory> Categories,
    IReadOnlyList<PublicArticleSummary> Articles, IReadOnlyList<PublicArticleDetail> Details);
