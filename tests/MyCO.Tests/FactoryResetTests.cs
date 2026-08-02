using MyCO.Configuration;

namespace MyCO.Tests;

public sealed class FactoryResetTests
{
    [Fact]
    public void CommitRemovesOnlyKnownTargetsAndPreservesRootAndLegacySource()
    {
        using var root = new TempDirectory();
        var current = Path.Combine(root.Path, "Myco");
        var legacy = Path.Combine(root.Path, "MyCodex");
        Directory.CreateDirectory(current);
        Directory.CreateDirectory(legacy);
        var paths = new ConfigPaths(current, legacy);
        paths.EnsureDirectories();
        File.WriteAllText(paths.ConfigFile, "config");
        File.WriteAllText(paths.CalibrationFile, "calibration");
        File.WriteAllText(Path.Combine(paths.AvatarsDirectory, "avatar.png"), "avatar");
        File.WriteAllText(Path.Combine(paths.LogsDirectory, "privacy.log"), "log");
        File.WriteAllText(Path.Combine(paths.BackupsDirectory, "backup.json"), "backup");
        File.WriteAllText(Path.Combine(current, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(legacy, "legacy.txt"), "legacy");

        using var transaction = new FactoryResetService(paths).Stage();
        transaction.Commit();

        Assert.True(Directory.Exists(current));
        Assert.False(File.Exists(paths.ConfigFile));
        Assert.False(File.Exists(paths.CalibrationFile));
        Assert.False(Directory.Exists(paths.AvatarsDirectory));
        Assert.False(Directory.Exists(paths.LogsDirectory));
        Assert.False(Directory.Exists(paths.BackupsDirectory));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(current, "keep.txt")));
        Assert.Equal("legacy", File.ReadAllText(Path.Combine(legacy, "legacy.txt")));
        Assert.Empty(Directory.GetDirectories(current, ".myco-reset-*"));
    }

    [Fact]
    public void RollbackRemovesNewDefaultsAndRestoresStagedData()
    {
        using var root = new TempDirectory();
        var paths = new ConfigPaths(Path.Combine(root.Path, "Myco"), null);
        paths.EnsureDirectories();
        File.WriteAllText(paths.ConfigFile, "old-config");
        File.WriteAllText(Path.Combine(paths.AvatarsDirectory, "old.png"), "old-avatar");

        using var transaction = new FactoryResetService(paths).Stage();
        paths.EnsureDirectories();
        File.WriteAllText(paths.ConfigFile, "new-config");
        File.WriteAllText(Path.Combine(paths.AvatarsDirectory, "new.png"), "new-avatar");

        transaction.Rollback();

        Assert.Equal("old-config", File.ReadAllText(paths.ConfigFile));
        Assert.Equal(
            "old-avatar",
            File.ReadAllText(Path.Combine(paths.AvatarsDirectory, "old.png")));
        Assert.False(File.Exists(Path.Combine(paths.AvatarsDirectory, "new.png")));
        Assert.Empty(Directory.GetDirectories(paths.BaseDirectory, ".myco-reset-*"));
    }

    [Fact]
    public void ValidationRejectsEscapesAndReparsePoints()
    {
        using var root = new TempDirectory();
        var baseDirectory = Path.Combine(root.Path, "Myco");
        Directory.CreateDirectory(baseDirectory);

        Assert.Throws<InvalidOperationException>(() =>
            FactoryResetService.ValidateKnownTarget(
                baseDirectory,
                Path.Combine(root.Path, "outside"),
                FileAttributes.Normal));
        Assert.Throws<IOException>(() =>
            FactoryResetService.ValidateKnownTarget(
                baseDirectory,
                Path.Combine(baseDirectory, "avatars"),
                FileAttributes.Directory | FileAttributes.ReparsePoint));
    }
}
