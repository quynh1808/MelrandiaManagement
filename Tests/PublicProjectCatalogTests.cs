using MelrandiaManagement.Services;

namespace MelrandiaManagement.Tests;

/// <summary>
/// Protects the initial public project contract while the registry is still
/// in memory. These tests remain valid when persistence is introduced later.
/// </summary>
[TestClass]
public sealed class PublicProjectCatalogTests
{
    [TestMethod]
    public void PublishedCatalog_ContainsAmsAsFirstFeaturedProject()
    {
        var catalog = new PublicProjectCatalog();

        var project = catalog.GetPublishedProjects().First();

        Assert.AreEqual("ams", project.Id);
        Assert.AreEqual("01", project.Sequence);
        Assert.IsTrue(project.IsFeatured);
        Assert.AreEqual("Aquaculture Monitoring System", project.DisplayName);
    }

    [TestMethod]
    public void PublishedCatalog_ContainsAgricultureProject()
    {
        var catalog = new PublicProjectCatalog();

        var project = catalog.FindBySlug("agriculture-system");

        Assert.IsNotNull(project);
        Assert.AreEqual("02", project.Sequence);
        Assert.AreEqual("Agriculture System", project.DisplayName);
    }

    [TestMethod]
    public void FindBySlug_IsCaseInsensitive()
    {
        var catalog = new PublicProjectCatalog();

        var project = catalog.FindBySlug("AMS");

        Assert.IsNotNull(project);
        Assert.AreEqual("ams", project.Slug);
    }

    [TestMethod]
    public void FindBySlug_ReturnsNullForUnknownProject()
    {
        var catalog = new PublicProjectCatalog();

        Assert.IsNull(catalog.FindBySlug("not-defined"));
    }
}
