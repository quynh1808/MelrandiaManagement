using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;

namespace MelrandiaManagement.Services;

/// <summary>
/// Converts trusted Markdown syntax to public HTML while disabling raw HTML. Unsafe URL schemes
/// are rejected before rendering so an Administrator account cannot accidentally publish XSS.
/// </summary>
public sealed partial class ArticleMarkdownRenderer
{
    public const string RichTextHtmlMarker = "<!-- melrandia-rich-text-html -->";

    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();
    private readonly HtmlSanitizer htmlSanitizer = new();

    public string ToEditorMarkdown(string content) => TryGetRichTextHtml(content, out var html)
        ? new ReverseMarkdown.Converter().Convert(htmlSanitizer.Sanitize(html))
        : content;

    public string Render(string content)
    {
        if (TryGetRichTextHtml(content, out var html))
            return htmlSanitizer.Sanitize(html);

        return RenderMarkdown(content);
    }

    private string RenderMarkdown(string markdown)
    {
        if (UnsafeScheme().IsMatch(markdown))
            throw new InvalidOperationException("Nội dung chứa liên kết dùng protocol không an toàn.");
        return Markdown.ToHtml(markdown, pipeline);
    }

    private static bool TryGetRichTextHtml(string content, out string html)
    {
        if (content.StartsWith(RichTextHtmlMarker, StringComparison.Ordinal))
        {
            html = content[RichTextHtmlMarker.Length..].TrimStart('\r', '\n');
            return true;
        }

        html = string.Empty;
        return false;
    }

    [GeneratedRegex(@"\]\(\s*(?:javascript|vbscript|data):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeScheme();
}
