using System.Text.Json;
using System.Text.RegularExpressions;
using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Services;

public sealed record SaveArticleRequest(Guid? Id, Guid CategoryId, string Slug, string Title,
    string Summary, string Tags, string ContentMarkdown, string Status, DateTime? PublishedAtUtc,
    string CoverAlt, int Version, string? PresetCoverUrl = null, bool RemoveCover = false);

/// <summary>Editorial use cases, upload compensation, workflow validation and audit.</summary>
public sealed partial class ArticleAdministrationService(
    IDbContextFactory<ManagementDbContext> factory,
    ArticleMediaStorage mediaStorage,
    ArticleMarkdownRenderer markdownRenderer,
    PublicArticleCatalog publicCatalog,
    IOptions<ArticleMediaOptions> mediaOptions)
{
    public async Task<ArticleCountTrend> GetCountTrendAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var totalCount = await db.Articles.CountAsync(cancellationToken);
        var currentMonthCount = await db.Articles.CountAsync(
            x => x.CreatedAtUtc >= currentMonthStart && x.CreatedAtUtc < nextMonthStart, cancellationToken);
        var previousMonthCount = await db.Articles.CountAsync(
            x => x.CreatedAtUtc >= previousMonthStart && x.CreatedAtUtc < currentMonthStart, cancellationToken);

        return new ArticleCountTrend(totalCount, currentMonthCount - previousMonthCount);
    }

    public async Task<ArticleAdminSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var categories = await db.ArticleCategories.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
        var articles = await db.Articles.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => new ArticleAdminSummary(x.Id, x.Slug, x.Title, x.Summary, x.Status,
                x.Category.DisplayName, x.Author.DisplayName,
                x.Media.Where(m => m.IsCover).Select(m => m.PublicUrl).FirstOrDefault(),
                x.PublishedAtUtc, x.UpdatedAtUtc, x.Media.Count, x.Version))
            .ToListAsync(cancellationToken);
        return new ArticleAdminSnapshot(categories, articles);
    }

    public async Task<IReadOnlyList<ArticleCategoryAdminSummary>> GetCategorySnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.ArticleCategories.AsNoTracking().OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.DisplayName)
            .Select(x => new ArticleCategoryAdminSummary(x.Id, x.Slug, x.DisplayName, x.Summary,
                x.DisplayOrder, x.IsEnabled, x.Articles.Count,
                x.Articles.Count(article => article.Status == PortalArticleStatuses.Published ||
                    article.Status == PortalArticleStatuses.Scheduled && article.PublishedAtUtc <= DateTime.UtcNow)))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveCategoryAsync(SaveArticleCategoryRequest request, string actor,
        CancellationToken cancellationToken = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();
        var summary = request.Summary.Trim();
        if (!CategorySlugPattern().IsMatch(slug))
            throw new InvalidOperationException("Slug chỉ được gồm chữ thường không dấu, số và dấu gạch ngang.");
        if (displayName.Length is < 2 or > 120)
            throw new InvalidOperationException("Tên chuyên mục phải có từ 2 đến 120 ký tự.");
        if (summary.Length is < 10 or > 500)
            throw new InvalidOperationException("Mô tả phải có từ 10 đến 500 ký tự.");
        if (request.DisplayOrder is < 0 or > 9999)
            throw new InvalidOperationException("Thứ tự hiển thị phải nằm trong khoảng 0 đến 9999.");

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var category = request.Id.HasValue
            ? await db.ArticleCategories.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
            : null;
        if (request.Id.HasValue && category is null)
            throw new InvalidOperationException("Chuyên mục cần chỉnh sửa không còn tồn tại.");
        if (await db.ArticleCategories.AnyAsync(x => x.Slug == slug && (!request.Id.HasValue || x.Id != request.Id.Value),
                cancellationToken))
            throw new InvalidOperationException("Slug này đã được sử dụng bởi một chuyên mục khác.");

        var isNew = category is null;
        category ??= new PortalArticleCategory();
        var previous = isNew ? null : JsonSerializer.Serialize(new
        {
            category.Slug,
            category.DisplayName,
            category.Summary,
            category.DisplayOrder,
            category.IsEnabled
        });

        category.Slug = slug;
        category.DisplayName = displayName;
        category.Summary = summary;
        category.DisplayOrder = request.DisplayOrder;
        category.IsEnabled = request.IsEnabled;
        if (isNew) db.ArticleCategories.Add(category);

        db.AuditLogs.Add(new PortalAuditLog
        {
            Actor = actor,
            Action = isNew ? "article-category.create" : "article-category.update",
            EntityType = "article-category",
            EntityId = category.Id.ToString(),
            Detail = JsonSerializer.Serialize(new { previous, current = request with { Slug = slug, DisplayName = displayName, Summary = summary } })
        });
        await db.SaveChangesAsync(cancellationToken);
        publicCatalog.Invalidate();
    }

    public async Task SetCategoryEnabledAsync(Guid id, bool isEnabled, string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var category = await db.ArticleCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Chuyên mục không còn tồn tại.");
        if (category.IsEnabled == isEnabled)
            return;

        category.IsEnabled = isEnabled;
        db.AuditLogs.Add(new PortalAuditLog
        {
            Actor = actor,
            Action = isEnabled ? "article-category.enable" : "article-category.disable",
            EntityType = "article-category",
            EntityId = category.Id.ToString(),
            Detail = JsonSerializer.Serialize(new { category.Slug, category.DisplayName })
        });
        await db.SaveChangesAsync(cancellationToken);
        publicCatalog.Invalidate();
    }

    public async Task DeleteCategoryAsync(Guid id, string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var category = await db.ArticleCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Chuyên mục không còn tồn tại.");
        if (await db.Articles.AnyAsync(x => x.CategoryId == id, cancellationToken))
            throw new InvalidOperationException("Chuyên mục đang có bài viết. Hãy chuyển bài viết sang chuyên mục khác trước khi xóa, hoặc ẩn chuyên mục.");

        db.AuditLogs.Add(new PortalAuditLog
        {
            Actor = actor,
            Action = "article-category.delete",
            EntityType = "article-category",
            EntityId = category.Id.ToString(),
            Detail = JsonSerializer.Serialize(new { category.Slug, category.DisplayName })
        });
        db.ArticleCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        publicCatalog.Invalidate();
    }

    public async Task<ArticleAdminDetail?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ArticleAdminDetail(x.Id, x.CategoryId, x.Slug, x.Title, x.Summary,
                x.Tags, x.ContentMarkdown, x.Status, x.PublishedAtUtc, x.Author.DisplayName,
                x.Media.OrderBy(m => m.DisplayOrder).ToList(), x.Version))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> DuplicateAsync(Guid id, Guid authorId, string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var source = await db.Articles.AsNoTracking().Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Bài viết không còn tồn tại.");

        const string titleSuffix = " (Bản sao)";
        var title = source.Title[..Math.Min(source.Title.Length, 240 - titleSuffix.Length)] + titleSuffix;
        var slugSuffix = $"-copy-{Guid.NewGuid():N}"[..14];
        var slug = source.Slug[..Math.Min(source.Slug.Length, 160 - slugSuffix.Length)] + slugSuffix;
        var duplicate = new PortalArticle
        {
            CategoryId = source.CategoryId,
            AuthorId = authorId,
            Slug = slug,
            Title = title,
            Summary = source.Summary,
            Tags = source.Tags,
            ContentMarkdown = source.ContentMarkdown,
            Status = PortalArticleStatuses.Draft,
            Version = 1,
            Media = source.Media.Select(media => new PortalArticleMedia
            {
                StoragePath = media.StoragePath,
                PublicUrl = media.PublicUrl,
                OriginalFileName = media.OriginalFileName,
                MimeType = media.MimeType,
                FileSize = media.FileSize,
                AltText = media.AltText,
                Caption = media.Caption,
                IsCover = media.IsCover,
                DisplayOrder = media.DisplayOrder
            }).ToList()
        };

        db.Articles.Add(duplicate);
        db.AuditLogs.Add(new PortalAuditLog
        {
            Actor = actor,
            Action = "article.duplicate",
            EntityType = "article",
            EntityId = duplicate.Id.ToString(),
            Detail = JsonSerializer.Serialize(new { SourceArticleId = source.Id, duplicate.Slug })
        });
        await db.SaveChangesAsync(cancellationToken);
        publicCatalog.Invalidate();
        return duplicate.Id;
    }

    public async Task DeleteAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var article = await db.Articles.Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Bài viết không còn tồn tại.");
        var storagePaths = article.Media.Select(media => media.StoragePath)
            .Distinct(StringComparer.Ordinal).ToArray();

        db.AuditLogs.Add(new PortalAuditLog
        {
            Actor = actor,
            Action = "article.delete",
            EntityType = "article",
            EntityId = article.Id.ToString(),
            Detail = JsonSerializer.Serialize(new { article.Slug, article.Title })
        });
        db.Articles.Remove(article);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var storagePath in storagePaths)
        {
            if (!await db.ArticleMedia.AsNoTracking().AnyAsync(media => media.StoragePath == storagePath, cancellationToken))
                await mediaStorage.DeleteIfExistsAsync(storagePath);
        }

        publicCatalog.Invalidate();
    }

    public async Task<Guid> SaveAsync(SaveArticleRequest request, IReadOnlyList<IFormFile> coverFiles,
        IReadOnlyList<IFormFile> galleryFiles, Guid authorId, string actor,
        CancellationToken cancellationToken = default)
    {
        Validate(request, coverFiles, galleryFiles);
        _ = markdownRenderer.Render(request.ContentMarkdown);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var article = request.Id.HasValue
            ? await db.Articles.Include(x => x.Media).FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
            : null;
        if (request.Id.HasValue && article is null) throw new InvalidOperationException("Bài viết không còn tồn tại.");
        if (!await db.ArticleCategories.AnyAsync(x => x.Id == request.CategoryId &&
                (x.IsEnabled || article != null && article.CategoryId == x.Id), cancellationToken))
            throw new InvalidOperationException("Chuyên mục không tồn tại hoặc đã bị vô hiệu hóa.");

        var isNew = article is null;
        article ??= new PortalArticle { Id = Guid.NewGuid(), AuthorId = authorId };
        if (!isNew && article.Version != request.Version)
            throw new InvalidOperationException("Bài viết đã được chỉnh sửa ở phiên khác. Hãy tải lại trang.");
        if (article.Media.Count + coverFiles.Count + galleryFiles.Count > mediaOptions.Value.MaxFilesPerArticle)
            throw new InvalidOperationException($"Tổng số ảnh của một bài viết không được vượt quá {mediaOptions.Value.MaxFilesPerArticle}.");

        var stored = new List<StoredArticleMedia>();
        try
        {
            foreach (var file in coverFiles.Concat(galleryFiles))
                stored.Add(await mediaStorage.SaveAsync(file, article.Id, cancellationToken));

            article.CategoryId = request.CategoryId;
            var rawSlug = string.IsNullOrWhiteSpace(request.Slug) &&
                request.Status == PortalArticleStatuses.Draft
                ? isNew ? $"ban-nhap-{Guid.NewGuid():N}"[..21] : article.Slug
                : request.Slug.Trim().ToLowerInvariant();

            var uniqueSlug = rawSlug;
            var counter = 1;
            while (await db.Articles.AnyAsync(x => x.Slug == uniqueSlug && x.Id != article.Id, cancellationToken))
            {
                var suffix = $"-{++counter}";
                var prefixLength = Math.Min(rawSlug.Length, 160 - suffix.Length);
                uniqueSlug = $"{rawSlug[..prefixLength]}{suffix}";
            }
            article.Slug = uniqueSlug;
            article.Title = request.Title.Trim();
            article.Summary = request.Summary.Trim();
            article.Tags = NormalizeTags(request.Tags);
            article.ContentMarkdown = request.ContentMarkdown.Trim();
            article.Status = request.Status;
            article.PublishedAtUtc = PublicationTime(request);
            article.UpdatedAtUtc = DateTime.UtcNow;
            if (!isNew) article.Version++;
            if (isNew) db.Articles.Add(article);

            var nextOrder = article.Media.Count == 0 ? 0 : article.Media.Max(x => x.DisplayOrder) + 1;
            var coverCount = coverFiles.Count;
            if (coverCount > 0)
            {
                foreach (var oldCover in article.Media.Where(x => x.IsCover)) oldCover.IsCover = false;
            }
            else if (request.RemoveCover)
            {
                foreach (var oldCover in article.Media.Where(x => x.IsCover)) oldCover.IsCover = false;
            }
            else if (!string.IsNullOrWhiteSpace(request.PresetCoverUrl))
            {
                var presetMedia = await db.ArticleMedia.AsNoTracking().FirstOrDefaultAsync(x => x.PublicUrl == request.PresetCoverUrl, cancellationToken);
                if (presetMedia != null)
                {
                    foreach (var oldCover in article.Media.Where(x => x.IsCover)) oldCover.IsCover = false;
                    var existingInArticle = article.Media.FirstOrDefault(x => x.StoragePath == presetMedia.StoragePath);
                    if (existingInArticle != null)
                    {
                        existingInArticle.IsCover = true;
                        if (!string.IsNullOrWhiteSpace(request.CoverAlt)) existingInArticle.AltText = request.CoverAlt.Trim();
                    }
                    else
                    {
                        article.Media.Add(new PortalArticleMedia
                        {
                            StoragePath = presetMedia.StoragePath,
                            PublicUrl = presetMedia.PublicUrl,
                            OriginalFileName = presetMedia.OriginalFileName,
                            MimeType = presetMedia.MimeType,
                            FileSize = presetMedia.FileSize,
                            IsCover = true,
                            AltText = !string.IsNullOrWhiteSpace(request.CoverAlt)
                                ? request.CoverAlt.Trim() : presetMedia.AltText,
                            DisplayOrder = nextOrder++
                        });
                    }
                }
            }
            else
            {
                var existingCover = article.Media.FirstOrDefault(x => x.IsCover);
                if (existingCover != null && !string.IsNullOrWhiteSpace(request.CoverAlt))
                {
                    existingCover.AltText = request.CoverAlt.Trim();
                }
            }
            for (var index = 0; index < stored.Count; index++)
            {
                var file = stored[index];
                article.Media.Add(new PortalArticleMedia
                {
                    StoragePath = file.StoragePath,
                    PublicUrl = file.PublicUrl,
                    OriginalFileName = file.OriginalFileName,
                    MimeType = file.MimeType,
                    FileSize = file.FileSize,
                    IsCover = index < coverCount,
                    AltText = index < coverCount && !string.IsNullOrWhiteSpace(request.CoverAlt)
                        ? request.CoverAlt.Trim() : Path.GetFileNameWithoutExtension(file.OriginalFileName),
                    DisplayOrder = nextOrder++
                });
            }

            db.AuditLogs.Add(new PortalAuditLog
            {
                Actor = actor,
                Action = isNew ? "article.create" : "article.update",
                EntityType = "article",
                EntityId = article.Id.ToString(),
                Detail = JsonSerializer.Serialize(new { article.Slug, article.Status, article.CategoryId, UploadedMedia = stored.Count })
            });
            await db.SaveChangesAsync(cancellationToken);
            publicCatalog.Invalidate();
            return article.Id;
        }
        catch (DbUpdateException ex)
        {
            foreach (var file in stored)
                await mediaStorage.DeleteIfExistsAsync(file.StoragePath);
            if (ex.InnerException?.Message.Contains("IX_articles_Slug", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new InvalidOperationException("Đường dẫn (Slug) này đã tồn tại trong hệ thống. Vui lòng chọn đường dẫn khác.");
            }
            throw;
        }
        catch
        {
            foreach (var file in stored)
                await mediaStorage.DeleteIfExistsAsync(file.StoragePath);
            throw;
        }
    }

    private void Validate(SaveArticleRequest request, IReadOnlyList<IFormFile> coverFiles,
        IReadOnlyList<IFormFile> galleryFiles)
    {
        var isDraft = request.Status == PortalArticleStatuses.Draft;
        var slug = request.Slug.Trim().ToLowerInvariant();
        if ((!isDraft && string.IsNullOrWhiteSpace(slug)) ||
            !string.IsNullOrWhiteSpace(slug) && !SlugPattern().IsMatch(slug))
            throw new InvalidOperationException("Slug chỉ dùng chữ thường, số và dấu gạch ngang.");
        var titleLength = request.Title.Trim().Length;
        if (titleLength > 240 || !isDraft && titleLength < 5)
            throw new InvalidOperationException(isDraft ? "Tiêu đề không được vượt quá 240 ký tự." : "Tiêu đề phải có 5-240 ký tự.");
        var summaryLength = request.Summary.Trim().Length;
        if (summaryLength > 700 || !isDraft && summaryLength < 20)
            throw new InvalidOperationException(isDraft ? "Mô tả ngắn không được vượt quá 700 ký tự." : "Mô tả ngắn phải có 20-700 ký tự.");
        var tags = request.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tags.Length > 20) throw new InvalidOperationException("Bài viết chỉ được có tối đa 20 thẻ.");
        if (tags.Any(tag => tag.Length > 40)) throw new InvalidOperationException("Mỗi thẻ không được vượt quá 40 ký tự.");
        if (string.Join(", ", tags.Distinct(StringComparer.OrdinalIgnoreCase)).Length > 1000)
            throw new InvalidOperationException("Tổng độ dài thẻ không được vượt quá 1000 ký tự.");
        var contentLength = request.ContentMarkdown.Trim().Length;
        if (contentLength > 200_000 || !isDraft && contentLength < 50)
            throw new InvalidOperationException(isDraft ? "Nội dung không được vượt quá 200.000 ký tự." : "Nội dung phải có 50-200.000 ký tự.");
        if (!PortalArticleStatuses.All.Contains(request.Status, StringComparer.Ordinal)) throw new InvalidOperationException("Trạng thái bài viết không hợp lệ.");
        if (coverFiles.Count > 1) throw new InvalidOperationException("Mỗi bài viết chỉ có một ảnh đại diện mới trong một lần lưu.");
        if (coverFiles.Count + galleryFiles.Count > mediaOptions.Value.MaxFilesPerArticle)
            throw new InvalidOperationException($"Mỗi lần chỉ được tải tối đa {mediaOptions.Value.MaxFilesPerArticle} ảnh.");
        if (request.Status == PortalArticleStatuses.Scheduled &&
            (!request.PublishedAtUtc.HasValue || request.PublishedAtUtc <= DateTime.UtcNow))
            throw new InvalidOperationException("Bài viết hẹn giờ phải có thời gian UTC trong tương lai.");
    }

    private static DateTime? PublicationTime(SaveArticleRequest request) => request.Status switch
    {
        // Published means "now"; a future timestamp must use the explicit Scheduled state.
        PortalArticleStatuses.Published => DateTime.UtcNow,
        PortalArticleStatuses.Scheduled => request.PublishedAtUtc,
        _ => null
    };

    private static string NormalizeTags(string tags) => string.Join(", ",
        tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,158}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,78}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex CategorySlugPattern();
}
