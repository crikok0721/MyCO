// Centralizes every per-user file under %APPDATA%\Myco by default.
namespace MyCO.Configuration;

public sealed class ConfigPaths
{
    private const string CurrentDirectoryName = "Myco";
    internal const string LegacyDirectoryName = "MyCodex";
    private const string MigrationLogName = "brand-migration.log";

    public ConfigPaths(
        string? baseDirectory = null,
        string? legacyBaseDirectory = null)
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        var usesDefaultDirectory = baseDirectory is null;
        BaseDirectory = baseDirectory ??
                        Path.Combine(applicationData, CurrentDirectoryName);
        LegacyBaseDirectory = legacyBaseDirectory ??
                              (usesDefaultDirectory
                                  ? Path.Combine(applicationData, LegacyDirectoryName)
                                  : null);
        MigrationResult = LegacyBrandDataMigrator.TryCopy(
            LegacyBaseDirectory,
            BaseDirectory);
    }

    public string BaseDirectory { get; }
    internal string? LegacyBaseDirectory { get; }
    internal BrandDataMigrationResult MigrationResult { get; }
    public string ConfigFile => Path.Combine(BaseDirectory, "config.json");
    public string CalibrationFile => Path.Combine(BaseDirectory, "calibration.json");
    public string AvatarsDirectory => Path.Combine(BaseDirectory, "avatars");
    public string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    public string BackupsDirectory => Path.Combine(BaseDirectory, "backups");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(AvatarsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        WriteMigrationLog();
    }

    internal string MapLegacyPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.IsNullOrWhiteSpace(LegacyBaseDirectory))
        {
            return value;
        }

        try
        {
            var legacyRoot = Path.GetFullPath(LegacyBaseDirectory);
            var source = Path.GetFullPath(value);
            var relative = Path.GetRelativePath(legacyRoot, source);
            if (relative == ".." ||
                relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                return value;
            }

            var destination = Path.GetFullPath(Path.Combine(BaseDirectory, relative));
            return File.Exists(destination) ? destination : value;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return value;
        }
    }

    private void WriteMigrationLog()
    {
        if (!MigrationResult.Attempted)
        {
            return;
        }

        var message = MigrationResult.Succeeded
            ? "MyCO copied the legacy MyCodex user data into the new data directory. The legacy directory was preserved."
            : $"MyCO could not copy the legacy MyCodex user data ({MigrationResult.ErrorCode}). The legacy directory was preserved and startup continued.";
        try
        {
            File.WriteAllText(
                Path.Combine(LogsDirectory, MigrationLogName),
                message);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A migration log must never make configuration startup fail.
        }
    }
}

internal sealed record BrandDataMigrationResult(
    bool Attempted,
    bool Succeeded,
    string? ErrorCode)
{
    public static BrandDataMigrationResult NotNeeded { get; } =
        new(false, false, null);
}

internal static class LegacyBrandDataMigrator
{
    public static BrandDataMigrationResult TryCopy(
        string? legacyDirectory,
        string currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(legacyDirectory) ||
            Directory.Exists(currentDirectory) ||
            !Directory.Exists(legacyDirectory))
        {
            return BrandDataMigrationResult.NotNeeded;
        }

        var destination = Path.GetFullPath(currentDirectory);
        var parent = Directory.GetParent(destination)?.FullName;
        if (string.IsNullOrWhiteSpace(parent))
        {
            return new BrandDataMigrationResult(true, false, "invalid-destination");
        }

        var staging = Path.Combine(
            parent,
            $".myco-migration-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(
                Path.GetFullPath(legacyDirectory),
                staging);
            if (Directory.Exists(destination))
            {
                TryDeleteStaging(staging);
                return BrandDataMigrationResult.NotNeeded;
            }
            Directory.Move(staging, destination);
            return new BrandDataMigrationResult(true, true, null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            TryDeleteStaging(staging);
            return new BrandDataMigrationResult(
                true,
                false,
                exception.GetType().Name);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        var sourceInfo = new DirectoryInfo(source);
        if ((sourceInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Legacy data root cannot be a reparse point.");
        }

        Directory.CreateDirectory(destination);
        foreach (var file in sourceInfo.EnumerateFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Legacy data cannot contain reparse points.");
            }
            file.CopyTo(Path.Combine(destination, file.Name), overwrite: false);
        }
        foreach (var directory in sourceInfo.EnumerateDirectories())
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Legacy data cannot contain reparse points.");
            }
            CopyDirectory(
                directory.FullName,
                Path.Combine(destination, directory.Name));
        }
    }

    private static void TryDeleteStaging(string staging)
    {
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The staging directory contains only copies created by this attempt.
        }
    }
}
