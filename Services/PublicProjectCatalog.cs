using MelrandiaManagement.Models;

namespace MelrandiaManagement.Services;

/// <summary>
/// Supplies public project metadata without coupling MelrandiaManagement to an
/// AMS database. A persistent registry can replace this implementation later.
/// </summary>
public sealed class PublicProjectCatalog
{
    private static readonly IReadOnlyList<PublicProjectPreview> Projects =
    [
        new(
            "ams",
            "ams",
            "AMS",
            "Aquaculture Monitoring System",
            "Hệ thống quản lý, giám sát cảm biến, thiết bị và hoạt động nuôi trồng thủy sản.",
            "Đang phát triển",
            "01",
            true),
        new(
            "agriculture-system",
            "agriculture-system",
            "AGS",
            "Agriculture System",
            "Không gian dự án dành cho giám sát môi trường, cây trồng và tự động hóa nông nghiệp thông minh.",
            "Định hướng",
            "02",
            true)
    ];

    public IReadOnlyList<PublicProjectPreview> GetPublishedProjects() => Projects;

    public PublicProjectPreview? FindBySlug(string slug) => Projects.FirstOrDefault(
        project => project.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
