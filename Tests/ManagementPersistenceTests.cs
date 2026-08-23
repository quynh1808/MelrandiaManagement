using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace MelrandiaManagement.Tests;

/// <summary>
/// Protects the MM persistence contract without requiring a running PostgreSQL
/// instance. Integration with a real server is covered by the documented
/// migration and readiness checks.
/// </summary>
[TestClass]
public sealed class ManagementPersistenceTests
{
    [TestMethod]
    public void ProjectRegistry_UsesUniqueProjectKeyAndSlug()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ManagedProject));

        Assert.IsNotNull(entity);
        var uniqueIndexes = entity.GetIndexes().Where(index => index.IsUnique)
            .Select(index => string.Join(',', index.Properties.Select(property => property.Name)))
            .ToArray();
        CollectionAssert.Contains(uniqueIndexes, nameof(ManagedProject.ProjectKey));
        CollectionAssert.Contains(uniqueIndexes, nameof(ManagedProject.Slug));
    }

    [TestMethod]
    public void ProjectAccess_UsesUserAndProjectAsCompositeKey()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(UserProjectAccess));

        Assert.IsNotNull(entity);
        CollectionAssert.AreEqual(
            new[] { nameof(UserProjectAccess.UserId), nameof(UserProjectAccess.ProjectId) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void ArticlePublishing_UsesUniqueSlugsAndPersistentMediaRelationship()
    {
        using var context = CreateContext();
        var article = context.Model.FindEntityType(typeof(PortalArticle));
        var category = context.Model.FindEntityType(typeof(PortalArticleCategory));
        var media = context.Model.FindEntityType(typeof(PortalArticleMedia));

        Assert.IsNotNull(article); Assert.IsNotNull(category); Assert.IsNotNull(media);
        Assert.IsTrue(article.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == nameof(PortalArticle.Slug)));
        Assert.IsTrue(category.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == nameof(PortalArticleCategory.Slug)));
        Assert.IsTrue(media.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(PortalArticle)));
    }

    [TestMethod]
    public void PostgreSqlOptions_RejectEmptyPassword()
    {
        var result = new PostgreSqlOptionsValidator().Validate(null, new PostgreSqlOptions
        {
            Password = string.Empty
        });

        Assert.IsTrue(result.Failed);
        StringAssert.Contains(result.FailureMessage, "Password");
    }

    [TestMethod]
    public void InitialProjectOptions_RejectUnsafePollingInterval()
    {
        var result = new InitialProjectOptionsValidator().Validate(null, new InitialProjectOptions
        {
            HealthCheckIntervalSeconds = 1
        });

        Assert.IsTrue(result.Failed);
        StringAssert.Contains(result.FailureMessage, "HealthCheckIntervalSeconds");
    }

    [TestMethod]
    public void PortalPerformanceOptions_RejectExcessiveCacheDuration()
    {
        var result = new PortalPerformanceOptionsValidator().Validate(null, new PortalPerformanceOptions
        {
            PublicProjectCacheSeconds = 3600
        });

        Assert.IsTrue(result.Failed);
        StringAssert.Contains(result.FailureMessage, "PublicProjectCacheSeconds");
    }

    private static ManagementDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_contract;Username=test;Password=test")
            .Options;
        return new ManagementDbContext(options);
    }
}
