using MelrandiaManagement.Services;

namespace MelrandiaManagement.Tests;

/// <summary>
/// Bảo vệ các URL chuyên mục công khai trước khi catalog được chuyển sang CMS.
/// </summary>
[TestClass]
public sealed class PublicArticleCatalogTests
{
    [TestMethod]
    public void Categories_ContainExpectedVietnameseSections()
    {
        var catalog = new PublicArticleCatalog();

        var slugs = catalog.GetCategories().Select(category => category.Slug).ToArray();

        CollectionAssert.AreEqual(new[] { "kinh-te", "cong-nghe", "gioi-thieu-sach" }, slugs);
    }

    [TestMethod]
    public void FindBySlug_IsCaseInsensitive()
    {
        var catalog = new PublicArticleCatalog();

        var category = catalog.FindBySlug("CONG-NGHE");

        Assert.IsNotNull(category);
        Assert.AreEqual("Công nghệ", category.DisplayName);
    }
}
