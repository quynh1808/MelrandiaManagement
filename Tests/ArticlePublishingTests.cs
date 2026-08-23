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
