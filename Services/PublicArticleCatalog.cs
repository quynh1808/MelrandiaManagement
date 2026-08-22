using MelrandiaManagement.Models;

namespace MelrandiaManagement.Services;

/// <summary>
/// Cung cấp danh mục bài viết công khai mà không phụ thuộc database. Dịch vụ
/// này giữ URL ổn định trước khi hệ thống quản trị nội dung được triển khai.
/// </summary>
public sealed class PublicArticleCatalog
{
    private static readonly IReadOnlyList<PublicArticleCategory> Categories =
    [
        new("kinh-te", "Kinh tế", "Góc nhìn kinh tế, thị trường và giá trị thực tiễn của các mô hình sản xuất.", "01"),
        new("cong-nghe", "Công nghệ", "Những ghi chép về Internet of Things, phần mềm, phần cứng và tự động hóa.", "02"),
        new("gioi-thieu-sach", "Giới thiệu sách", "Nơi giới thiệu những cuốn sách đáng đọc cùng các ý tưởng có thể áp dụng.", "03")
    ];

    public IReadOnlyList<PublicArticleCategory> GetCategories() => Categories;

    public PublicArticleCategory? FindBySlug(string slug) => Categories.FirstOrDefault(
        category => category.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
