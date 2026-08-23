using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using MelrandiaManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Tests;

/// <summary>Protects query reuse and immediate invalidation of public project metadata.</summary>
[TestClass]
public sealed class PublicProjectCatalogCacheTests
{
    [TestMethod]
    public async Task Catalog_ReusesSnapshotUntilExplicitlyInvalidated()
    {
        var databaseName = $"public-project-cache-{Guid.NewGuid()}";
        var dbOptions = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using (var seed = new ManagementDbContext(dbOptions))
        {
            seed.Projects.Add(new ManagedProject
            {
                ProjectKey = "ams",
                Slug = "ams",
                ShortName = "AMS",
                DisplayName = "Aquaculture Monitoring System",
                IsPublic = true,
                DisplayOrder = 1
            });
            await seed.SaveChangesAsync();
        }

        var factory = new CountingDbContextFactory(dbOptions);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new PublicProjectCatalog(factory, cache,
            Options.Create(new PortalPerformanceOptions { PublicProjectCacheSeconds = 30 }));

        var first = await catalog.GetPublishedProjectsAsync();
        var second = await catalog.GetPublishedProjectsAsync();
        Assert.AreSame(first, second);
        Assert.AreEqual(1, factory.CreateCount);

        await using (var update = new ManagementDbContext(dbOptions))
        {
            var project = await update.Projects.SingleAsync();
            project.DisplayName = "AMS updated";
            await update.SaveChangesAsync();
        }

        Assert.AreEqual("Aquaculture Monitoring System",
            (await catalog.FindBySlugAsync("AMS"))!.DisplayName);
        catalog.Invalidate();
        Assert.AreEqual("AMS updated", (await catalog.FindBySlugAsync("ams"))!.DisplayName);
        Assert.AreEqual(2, factory.CreateCount);
    }

    private sealed class CountingDbContextFactory(DbContextOptions<ManagementDbContext> options)
        : IDbContextFactory<ManagementDbContext>
    {
        public int CreateCount { get; private set; }

        public ManagementDbContext CreateDbContext()
        {
            CreateCount++;
            return new ManagementDbContext(options);
        }
    }
}
