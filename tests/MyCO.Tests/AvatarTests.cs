using MyCO.Avatars;
using MyCO.Configuration;

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
    public async Task CorruptPackagedDefaultAvatarFailsWithoutWritingManagedFile()
    {
        using var directory = new TempDirectory();
        var avatars = Path.Combine(directory.Path, "avatars");
        var service = new AvatarService(avatars);
        await using var corrupt = new MemoryStream(
            "not an image"u8.ToArray(),
            writable: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            DefaultAvatarAsset.SeedAsync(AppConfig.Default, service, corrupt));

        Assert.False(Directory.Exists(avatars));
    }

    [Fact]
    public async Task AvatarImportAcceptsValidatedCroppedStream()
    {
        using var directory = new TempDirectory();
        var service = new AvatarService(
            System.IO.Path.Combine(directory.Path, "avatars"));
        await using var stream = new MemoryStream(
            Convert.FromBase64String(OnePixelPng),
            writable: false);

        var result = await service.ImportAsync(stream);

        Assert.True(File.Exists(result.StoredPath));
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(68, result.Length);
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

    [Fact]
    public async Task PackagedDefaultAvatarIsSeededIntoManagedStorageWithoutOverwritingCustomIdentity()
    {
        using var directory = new TempDirectory();
        var avatars = System.IO.Path.Combine(directory.Path, "avatars");
        var service = new AvatarService(avatars);
        var root = FindRepositoryRoot();
        await using var stream = File.OpenRead(
            System.IO.Path.Combine(root, "assets", DefaultAvatarAsset.FileName));

        var seeded = await DefaultAvatarAsset.SeedAsync(
            AppConfig.Default,
            service,
            stream);

        Assert.Equal("菲叶子", seeded.Assistant.Name);
        Assert.StartsWith(avatars, seeded.Assistant.Avatar, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(seeded.Assistant.Avatar));

        var custom = AppConfig.Default with
        {
            Assistant = new PersonConfig
            {
                Name = "自定义",
                Avatar = seeded.Assistant.Avatar
            }
        };
        await using var secondStream = File.OpenRead(
            System.IO.Path.Combine(root, "assets", DefaultAvatarAsset.FileName));
        var preserved = await DefaultAvatarAsset.SeedAsync(
            custom,
            service,
            secondStream);

        Assert.Equal(custom.Assistant, preserved.Assistant);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyCO.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("MyCO repository root was not found.");
    }
}
