using System.Text.RegularExpressions;
using Markdig;

namespace MelrandiaManagement.Services;

/// <summary>
/// Converts trusted Markdown syntax to public HTML while disabling raw HTML. Unsafe URL schemes
/// are rejected before rendering so an Administrator account cannot accidentally publish XSS.
/// </summary>
public sealed partial class ArticleMarkdownRenderer
{
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public string Render(string markdown)
    {
        if (UnsafeScheme().IsMatch(markdown))
            throw new InvalidOperationException("Nội dung chứa liên kết dùng protocol không an toàn.");
        return Markdown.ToHtml(markdown, pipeline);
    }

    [GeneratedRegex(@"\]\(\s*(?:javascript|vbscript|data):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeScheme();
}
