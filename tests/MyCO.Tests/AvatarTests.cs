using MyCO.Avatars;

// Verifies content-based avatar import, data URLs, and format rejection.
namespace MyCO.Tests;

public sealed class AvatarTests
{
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    [Fact]
    public async Task AvatarImportCopiesToManagedDirectoryAndBuildsDataUrl()
    {
        using var directory = new TempDirectory();
        var source = System.IO.Path.Combine(directory.Path, "avatar.any");
        await File.WriteAllBytesAsync(source, Convert.FromBase64String(OnePixelPng));
        var avatars = System.IO.Path.Combine(directory.Path, "avatars");
        var service = new AvatarService(avatars);

        var result = await service.ImportAsync(source);
        var dataUrl = await service.ToDataUrlAsync(result.StoredPath);

        Assert.True(File.Exists(result.StoredPath));
        Assert.StartsWith(avatars, result.StoredPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("image/png", result.MediaType);
        Assert.StartsWith("data:image/png;base64,", dataUrl);
    }

    [Fact]
    public async Task AvatarImportRejectsUnknownFormats()
    {
        using var directory = new TempDirectory();
        var source = System.IO.Path.Combine(directory.Path, "avatar.txt");
        await File.WriteAllTextAsync(source, "not an image");
        var service = new AvatarService(System.IO.Path.Combine(directory.Path, "avatars"));

        await Assert.ThrowsAsync<ArgumentException>(() => service.ImportAsync(source));
    }

    [Fact]
    public async Task DataUrlRejectsFilesOutsideManagedDirectory()
    {
        using var directory = new TempDirectory();
        var source = System.IO.Path.Combine(directory.Path, "external.png");
        await File.WriteAllBytesAsync(source, Convert.FromBase64String(OnePixelPng));
        var service = new AvatarService(
            System.IO.Path.Combine(directory.Path, "avatars"));

        Assert.Equal(string.Empty, await service.ToDataUrlAsync(source));
    }
}
