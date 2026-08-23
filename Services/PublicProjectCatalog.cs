using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Services;

/// <summary>
/// Read-only adapter from the durable Project Registry to public presentation models. A short
/// process-local cache prevents the header and page body from issuing the same query while an
/// explicit invalidation keeps administrator changes immediately visible on this instance.
/// </summary>
public sealed class PublicProjectCatalog(
    IDbContextFactory<ManagementDbContext> factory,
    IMemoryCache cache,
    IOptions<PortalPerformanceOptions> performanceOptions)
{
    private const string PublishedProjectsCacheKey = "public-project-catalog:v1";
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    public async Task<IReadOnlyList<PublicProjectPreview>> GetPublishedProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<IReadOnlyList<PublicProjectPreview>>(
                PublishedProjectsCacheKey, out var cached) && cached is not null)
            return cached;

        // IMemoryCache does not serialize concurrent cache misses. The gate
        // prevents a cold page burst from issuing identical PostgreSQL queries.
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<IReadOnlyList<PublicProjectPreview>>(
                    PublishedProjectsCacheKey, out cached) && cached is not null)
                return cached;

            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var projects = await db.Projects.AsNoTracking()
                .Where(project => project.IsPublic)
                .OrderBy(project => project.DisplayOrder)
                .Select(project => new PublicProjectPreview(
                    project.ProjectKey, project.Slug, project.ShortName, project.DisplayName,
                    project.Description, project.LifecycleStatus, project.DisplayOrder.ToString("00"),
                    project.IsFeatured, project.IsEnabled, project.PublicUrl))
                .ToArrayAsync(cancellationToken);

            cache.Set(PublishedProjectsCacheKey, projects,
                TimeSpan.FromSeconds(performanceOptions.Value.PublicProjectCacheSeconds));
            return projects;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public async Task<PublicProjectPreview?> FindBySlugAsync(string slug,
        CancellationToken cancellationToken = default)
    {
        var projects = await GetPublishedProjectsAsync(cancellationToken);
        return projects.FirstOrDefault(project =>
            project.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Removes only public registry metadata; no identity or child-project data is cached.</summary>
    public void Invalidate() => cache.Remove(PublishedProjectsCacheKey);
}
