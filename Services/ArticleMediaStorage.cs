using MelrandiaManagement.Configuration;
using Microsoft.Extensions.Options;

namespace MelrandiaManagement.Services;

public sealed record StoredArticleMedia(string StoragePath, string PublicUrl, string OriginalFileName,
    string MimeType, long FileSize);

/// <summary>
/// Writes validated image bytes to persistent storage. Browser filenames never become filesystem
/// names; a generated GUID and detected image signature determine the stored file.
/// </summary>
public sealed class ArticleMediaStorage(IHostEnvironment environment, IOptions<ArticleMediaOptions> options)
{
    private readonly ArticleMediaOptions settings = options.Value;
    public string RootPath { get; } = ResolveRoot(environment.ContentRootPath, options.Value.RootPath);

    public async Task<StoredArticleMedia> SaveAsync(IFormFile file, Guid articleId,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new InvalidOperationException("File ảnh đang trống.");
        if (file.Length > settings.MaxFileSizeMegabytes * 1024L * 1024L)
            throw new InvalidOperationException($"Mỗi ảnh không được vượt quá {settings.MaxFileSizeMegabytes} MB.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header, cancellationToken);
        var format = DetectFormat(header.AsSpan(0, read));
        if (format is null)
            throw new InvalidOperationException("Chỉ chấp nhận ảnh JPEG, PNG, GIF hoặc WebP hợp lệ.");

        var now = DateTime.UtcNow;
        var relativeDirectory = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), articleId.ToString("N"));
        var directory = Path.Combine(RootPath, relativeDirectory);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{format.Value.Extension}";
        var fullPath = Path.Combine(directory, fileName);

        input.Position = 0;
        await using (var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write,
                         FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            await input.CopyToAsync(output, cancellationToken);

        var storagePath = Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
        var originalName = Path.GetFileName(file.FileName);
        if (originalName.Length > 260) originalName = originalName[..260];
        return new StoredArticleMedia(storagePath, $"/media/{storagePath}",
            originalName, format.Value.MimeType, file.Length);
    }

    public Task DeleteIfExistsAsync(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Media path nằm ngoài storage root.");
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public static string ResolveRoot(string contentRoot, string configuredPath) =>
        Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRoot, configuredPath));

    private static (string Extension, string MimeType)? DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return (".jpg", "image/jpeg");
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return (".png", "image/png");
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
            return (".gif", "image/gif");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        return null;
    }
}
