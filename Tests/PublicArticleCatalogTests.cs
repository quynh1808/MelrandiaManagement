using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using MelrandiaManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MelrandiaManagement.Tests;

/// <summary>Protects public editorial visibility, category URLs and Markdown safety.</summary>
[TestClass]
public sealed class PublicArticleCatalogTests
{
    [TestMethod]
    public async Task Catalog_ReturnsCategoriesAndOnlyPublicArticles()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase($"articles-{Guid.NewGuid()}").Options;
        var category = new PortalArticleCategory { Slug = "cong-nghe", DisplayName = "Công nghệ", Summary = "IoT", DisplayOrder = 2 };
        var author = new PortalUser { Id = Guid.NewGuid(), UserName = "admin", DisplayName = "Quản trị" };
        await using (var db = new ManagementDbContext(options))
        {
            db.ArticleCategories.Add(category); db.Users.Add(author);
            db.Articles.AddRange(
                NewArticle(category.Id, author.Id, "published", PortalArticleStatuses.Published, DateTime.UtcNow),
                NewArticle(category.Id, author.Id, "draft", PortalArticleStatuses.Draft, null),
                NewArticle(category.Id, author.Id, "scheduled", PortalArticleStatuses.Scheduled, DateTime.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
        }
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new PublicArticleCatalog(new TestFactory(options), cache, new ArticleMarkdownRenderer());

        var categories = await catalog.GetCategoriesAsync();
        var articles = await catalog.GetPublishedArticlesAsync();

        Assert.AreEqual("cong-nghe", categories.Single().Slug);
        Assert.AreEqual(2, categories.Single().PublishedCount);
        CollectionAssert.AreEquivalent(new[] { "published", "scheduled" }, articles.Select(x => x.Slug).ToArray());
    }

    [TestMethod]
    public void MarkdownRenderer_RejectsUnsafeUrlSchemes()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new ArticleMarkdownRenderer().Render("[bad](javascript:alert(1))"));
    }

    private static PortalArticle NewArticle(Guid categoryId, Guid authorId, string slug,
        string status, DateTime? publishedAt) => new()
        {
            CategoryId = categoryId, AuthorId = authorId, Slug = slug, Title = $"Article {slug}",
            Summary = "A sufficiently long summary for the article.", ContentMarkdown = "# Heading\n\nSafe content.",
            Status = status, PublishedAtUtc = publishedAt
        };

    private sealed class TestFactory(DbContextOptions<ManagementDbContext> options)
        : IDbContextFactory<ManagementDbContext>
    {
        public ManagementDbContext CreateDbContext() => new(options);
    }
}
