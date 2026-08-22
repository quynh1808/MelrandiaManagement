namespace MelrandiaManagement.Models;

/// <summary>
/// Metadata công khai của một chuyên mục bài viết. Model này chỉ mô tả cấu
/// trúc điều hướng; nội dung xuất bản bền vững sẽ được bổ sung ở giai đoạn CMS.
/// </summary>
public sealed record PublicArticleCategory(
    string Slug,
    string DisplayName,
    string Summary,
    string Sequence);
