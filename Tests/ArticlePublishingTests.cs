using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using MelrandiaManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Tests;

/// <summary>Exercises article persistence and image bytes without touching production storage.</summary>
[TestClass]
public sealed class ArticlePublishingTests
{
    [TestMethod]
    public async Task DuplicateAndDeleteAsync_PreserveSharedMediaUntilLastArticleIsDeleted()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"mm-article-duplicate-{Guid.NewGuid():N}");
        var relativeMediaPath = "2026/10/source/cover.png";
        Directory.CreateDirectory(Path.Combine(temporaryRoot, Path.GetDirectoryName(relativeMediaPath)!));
        try
        {
            File.WriteAllBytes(Path.Combine(temporaryRoot, relativeMediaPath), [1, 2, 3]);
            var dbOptions = new DbContextOptionsBuilder<ManagementDbContext>()
                .UseInMemoryDatabase($"article-duplicate-{Guid.NewGuid()}").Options;
            var factory = new TestFactory(dbOptions);
            var category = new PortalArticleCategory { Slug = "khoa-hoc", DisplayName = "Khoa học", Summary = "Khoa học" };
            var author = new PortalUser { Id = Guid.NewGuid(), UserName = "admin", DisplayName = "Quản trị" };
            var source = new PortalArticle
            {
                CategoryId = category.Id,
                AuthorId = author.Id,
                Slug = "bai-viet-goc",
                Title = "Bài viết gốc",
                Summary = "Mô tả đủ dài cho bài viết gốc để kiểm tra nhân bản.",
                ContentMarkdown = "# Bài viết gốc\n\nNội dung đủ dài cho kiểm thử dịch vụ bài viết.",
                Status = PortalArticleStatuses.Published,
                PublishedAtUtc = DateTime.UtcNow,
                Media =
                [
                    new PortalArticleMedia
                    {
                        StoragePath = relativeMediaPath,
                        PublicUrl = $"/media/{relativeMediaPath}",
                        OriginalFileName = "cover.png",
                        MimeType = "image/png",
                        FileSize = 3,
                        IsCover = true
                    }
                ]
            };

            await using (var seed = new ManagementDbContext(dbOptions))
            {
                seed.ArticleCategories.Add(category);
                seed.Users.Add(author);
                seed.Articles.Add(source);
                await seed.SaveChangesAsync();
            }

            var mediaOptions = Options.Create(new ArticleMediaOptions { RootPath = temporaryRoot });
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var markdown = new ArticleMarkdownRenderer();
            var catalog = new PublicArticleCatalog(factory, cache, markdown);
            var service = new ArticleAdministrationService(factory,
                new ArticleMediaStorage(new TestEnvironment(temporaryRoot), mediaOptions),
                markdown, catalog, mediaOptions);

            var duplicateId = await service.DuplicateAsync(source.Id, author.Id, "admin");
            await using (var verifyDuplicate = new ManagementDbContext(dbOptions))
            {
                var duplicate = await verifyDuplicate.Articles.Include(x => x.Media).SingleAsync(x => x.Id == duplicateId);
                Assert.AreEqual(PortalArticleStatuses.Draft, duplicate.Status);
                Assert.HasCount(1, duplicate.Media);
                Assert.AreNotEqual(source.Slug, duplicate.Slug);
            }

            await service.DeleteAsync(source.Id, "admin");
            Assert.IsTrue(File.Exists(Path.Combine(temporaryRoot, relativeMediaPath)));

            await service.DeleteAsync(duplicateId, "admin");
            Assert.IsFalse(File.Exists(Path.Combine(temporaryRoot, relativeMediaPath)));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }
    }

    [TestMethod]
    public void RichTextHtml_IsSanitized_AndLegacyMarkdownStillRenders()
    {
        var renderer = new ArticleMarkdownRenderer();
        var html = "<p><strong>A safe formatted article containing enough useful text for the rich editor validation requirement.</strong></p><script>alert(1)</script><a href=\"javascript:alert(1)\">unsafe link</a>";

        var stored = $"{ArticleMarkdownRenderer.RichTextHtmlMarker}\n{html}";
        var rendered = renderer.Render(stored);
        var editableMarkdown = renderer.ToEditorMarkdown(stored);

        StringAssert.Contains(rendered, "<p>");
        StringAssert.Contains(rendered, "<strong>");
        Assert.IsFalse(rendered.Contains("<script", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(rendered.Contains("javascript:", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(editableMarkdown, "**A safe formatted article");
        StringAssert.Contains(renderer.Render("# Legacy title"), "<h1");
        StringAssert.Contains(renderer.Render("# Legacy title"), "Legacy title</h1>");
        StringAssert.Contains(renderer.ToEditorMarkdown("# Legacy title"), "# Legacy title");
    }

    [TestMethod]
    public async Task GetCountTrendAsync_ReturnsTotalAndMonthOverMonthChange()
    {
        var dbOptions = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase($"article-trend-{Guid.NewGuid()}").Options;
        var factory = new TestFactory(dbOptions);
        var category = new PortalArticleCategory { Slug = "cong-nghe", DisplayName = "Công nghệ", Summary = "IoT" };
        var author = new PortalUser { Id = Guid.NewGuid(), UserName = "admin", DisplayName = "Quản trị" };
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var seed = new ManagementDbContext(dbOptions))
        {
            seed.ArticleCategories.Add(category);
            seed.Users.Add(author);
            seed.Articles.AddRange(
                new PortalArticle { CategoryId = category.Id, AuthorId = author.Id, CreatedAtUtc = currentMonthStart.AddDays(1) },
                new PortalArticle { CategoryId = category.Id, AuthorId = author.Id, CreatedAtUtc = currentMonthStart.AddMonths(-1).AddDays(1) },
                new PortalArticle { CategoryId = category.Id, AuthorId = author.Id, CreatedAtUtc = currentMonthStart.AddMonths(-1).AddDays(2) },
                new PortalArticle { CategoryId = category.Id, AuthorId = author.Id, CreatedAtUtc = currentMonthStart.AddMonths(-2) });
            await seed.SaveChangesAsync();
        }

        var mediaOptions = Options.Create(new ArticleMediaOptions { RootPath = Path.GetTempPath() });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var markdown = new ArticleMarkdownRenderer();
        var catalog = new PublicArticleCatalog(factory, cache, markdown);
        var service = new ArticleAdministrationService(factory,
            new ArticleMediaStorage(new TestEnvironment(Path.GetTempPath()), mediaOptions),
            markdown, catalog, mediaOptions);

        var trend = await service.GetCountTrendAsync();

        Assert.AreEqual(4, trend.TotalCount);
        Assert.AreEqual(-1, trend.MonthOverMonthChange);
    }

    [TestMethod]
    public async Task SaveDraft_PersistsMetadataAndValidatedCoverFile()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"mm-article-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var dbOptions = new DbContextOptionsBuilder<ManagementDbContext>()
                .UseInMemoryDatabase($"article-save-{Guid.NewGuid()}").Options;
            var factory = new TestFactory(dbOptions);
            var category = new PortalArticleCategory { Slug = "cong-nghe", DisplayName = "Công nghệ", Summary = "IoT", DisplayOrder = 1 };
            var author = new PortalUser { Id = Guid.NewGuid(), UserName = "admin", DisplayName = "Quản trị" };
            await using (var seed = new ManagementDbContext(dbOptions))
            {
                seed.ArticleCategories.Add(category); seed.Users.Add(author); await seed.SaveChangesAsync();
            }
            var mediaOptions = Options.Create(new ArticleMediaOptions { RootPath = temporaryRoot, MaxFileSizeMegabytes = 1, MaxFilesPerArticle = 4 });
            var storage = new ArticleMediaStorage(new TestEnvironment(temporaryRoot), mediaOptions);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var markdown = new ArticleMarkdownRenderer();
            var catalog = new PublicArticleCatalog(factory, cache, markdown);
            var service = new ArticleAdministrationService(factory, storage, markdown, catalog, mediaOptions);
            var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0 };
            await using var stream = new MemoryStream(bytes);
            var cover = new FormFile(stream, 0, bytes.Length, "cover", "cover.png");

            var id = await service.SaveAsync(new SaveArticleRequest(null, category.Id, "bai-viet-thu",
                "Bài viết thử nghiệm", "Mô tả đủ dài cho bài viết thử nghiệm trong unit test.",
                "# Nội dung\n\nĐây là nội dung Markdown an toàn và đủ dài để vượt qua validation.",
                PortalArticleStatuses.Draft, null, "Ảnh thử nghiệm", 1), [cover], [], author.Id, "admin");

            await using var verify = new ManagementDbContext(dbOptions);
            var article = await verify.Articles.Include(x => x.Media).SingleAsync(x => x.Id == id);
            Assert.AreEqual(PortalArticleStatuses.Draft, article.Status);
            Assert.HasCount(1, article.Media);
            Assert.IsTrue(File.Exists(Path.Combine(temporaryRoot, article.Media.Single().StoragePath.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }
    }

    private sealed class TestFactory(DbContextOptions<ManagementDbContext> options) : IDbContextFactory<ManagementDbContext>
    {
        public ManagementDbContext CreateDbContext() => new(options);
    }

    private sealed class TestEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "MelrandiaManagement.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
