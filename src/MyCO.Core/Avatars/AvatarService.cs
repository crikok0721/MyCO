using System.Security.Cryptography;
using System.Text.RegularExpressions;

// Imports safe image formats into MyCO's managed local data directory.
namespace MyCO.Avatars;

public sealed record AvatarImportResult(
    string StoredPath,
    string MediaType,
    long Length);

public sealed partial class AvatarService
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

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        var (extension, mediaType) = DetectFormat(
            bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        ValidateDimensions(bytes, extension);
        var hash = Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();

        Directory.CreateDirectory(_avatarsDirectory);
        // Content-based names deduplicate repeated imports and avoid user-controlled filenames.
        var destination = Path.Combine(_avatarsDirectory, $"{hash[..24]}{extension}");
        if (!File.Exists(destination))
        {
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        return new AvatarImportResult(destination, mediaType, file.Length);
    }

    public async Task<string> ToDataUrlAsync(
        string? storedPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return string.Empty;
        }

        var root = Path.GetFullPath(_avatarsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(storedPath);
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !ManagedFileName().IsMatch(Path.GetFileName(candidate)) ||
            !File.Exists(candidate))
        {
            return string.Empty;
        }
        var rootAttributes = File.GetAttributes(_avatarsDirectory);
        var fileAttributes = File.GetAttributes(candidate);
        if (rootAttributes.HasFlag(FileAttributes.ReparsePoint) ||
            fileAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return string.Empty;
        }
        var file = new FileInfo(candidate);
        if (file.Length is <= 0 or > MaximumBytes)
        {
            return string.Empty;
        }

        // A data URL lets the injected page render the image without filesystem access.
        var bytes = await File.ReadAllBytesAsync(candidate, cancellationToken)
            .ConfigureAwait(false);
        var (extension, mediaType) =
            DetectFormat(bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        if (!candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        ValidateDimensions(bytes, extension);
        return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static void ValidateDimensions(byte[] bytes, string extension)
    {
        var (width, height) = extension switch
        {
            ".png" when bytes.Length >= 24 =>
                (ReadBigEndianInt32(bytes, 16), ReadBigEndianInt32(bytes, 20)),
            ".gif" when bytes.Length >= 10 =>
                (BitConverter.ToUInt16(bytes, 6), BitConverter.ToUInt16(bytes, 8)),
            ".bmp" when bytes.Length >= 26 =>
                (BitConverter.ToInt32(bytes, 18), Math.Abs(BitConverter.ToInt32(bytes, 22))),
            ".jpg" => ReadJpegDimensions(bytes),
            _ => (0, 0)
        };
        if (width is <= 0 or > 4096 ||
            height is <= 0 or > 4096 ||
            (long)width * height > 16_777_216)
        {
            throw new ArgumentException("Avatar dimensions must not exceed 4096 x 4096.");
        }
    }

    private static (int Width, int Height) ReadJpegDimensions(byte[] bytes)
    {
        var offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset] != 0xff)
            {
                offset++;
                continue;
            }
            var marker = bytes[offset + 1];
            offset += 2;
            if (marker is 0xd8 or 0xd9)
            {
                continue;
            }
            if (offset + 2 > bytes.Length)
            {
                break;
            }
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            if (length < 2 || offset + length > bytes.Length)
            {
                break;
            }
            if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or
                >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
            {
                return (
                    (bytes[offset + 5] << 8) | bytes[offset + 6],
                    (bytes[offset + 3] << 8) | bytes[offset + 4]);
            }
            offset += length;
        }
        return (0, 0);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) |
               (bytes[offset + 1] << 16) |
               (bytes[offset + 2] << 8) |
               bytes[offset + 3];
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

    [GeneratedRegex(
        "^[0-9a-f]{24}\\.(png|jpg|gif|bmp)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ManagedFileName();
}
