using System.Security.Cryptography;

// Imports safe image formats into MyCodex's managed local data directory.
namespace MyCodex.Avatars;

public sealed record AvatarImportResult(
    string StoredPath,
    string MediaType,
    long Length);

public sealed class AvatarService
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private readonly string _avatarsDirectory;

    public AvatarService(string avatarsDirectory)
    {
        _avatarsDirectory = avatarsDirectory;
    }

    public async Task<AvatarImportResult> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Avatar image was not found.", sourcePath);
        }

        var file = new FileInfo(sourcePath);
        if (file.Length is <= 0 or > MaximumBytes)
        {
            throw new ArgumentException("Avatar image must be between 1 byte and 10 MiB.");
        }

        // Detect the real format from magic bytes instead of trusting the file extension.
        await using var input = File.OpenRead(sourcePath);
        var header = new byte[Math.Min(16, (int)file.Length)];
        var read = await input.ReadAsync(header, cancellationToken).ConfigureAwait(false);
        var (extension, mediaType) = DetectFormat(header.AsSpan(0, read));
        input.Position = 0;
        var hash = Convert.ToHexString(
                await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();

        Directory.CreateDirectory(_avatarsDirectory);
        // Content-based names deduplicate repeated imports and avoid user-controlled filenames.
        var destination = Path.Combine(_avatarsDirectory, $"{hash[..24]}{extension}");
        if (!File.Exists(destination))
        {
            input.Position = 0;
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        return new AvatarImportResult(destination, mediaType, file.Length);
    }

    public static async Task<string> ToDataUrlAsync(
        string? storedPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath))
        {
            return string.Empty;
        }

        // A data URL lets the injected page render the image without filesystem access.
        var bytes = await File.ReadAllBytesAsync(storedPath, cancellationToken)
            .ConfigureAwait(false);
        var (_, mediaType) = DetectFormat(bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static (string Extension, string MediaType) DetectFormat(
        ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 &&
            header[..8].SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
        {
            return (".png", "image/png");
        }
        if (header.Length >= 3 &&
            header[0] == 0xff &&
            header[1] == 0xd8 &&
            header[2] == 0xff)
        {
            return (".jpg", "image/jpeg");
        }
        if (header.Length >= 6 &&
            (header[..6].SequenceEqual("GIF87a"u8) ||
             header[..6].SequenceEqual("GIF89a"u8)))
        {
            return (".gif", "image/gif");
        }
        if (header.Length >= 2 && header[..2].SequenceEqual("BM"u8))
        {
            return (".bmp", "image/bmp");
        }

        throw new ArgumentException("Avatar must be a PNG, JPEG, GIF, or BMP image.");
    }
}
