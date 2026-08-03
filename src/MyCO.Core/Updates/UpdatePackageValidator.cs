using System.IO.Compression;
using System.Security.Cryptography;

namespace MyCO.Updates;

public sealed record UpdatePackageManifest(
    IReadOnlyList<string> Files,
    string ExecutablePath,
    string ExecutableSha256);

public static class UpdatePackageValidator
{
    public const long MaxArchiveBytes = 512L * 1024 * 1024;
    public const long MaxExpandedBytes = 1024L * 1024 * 1024;
    public const long MaxHashBytes = 64 * 1024;
    private const int MaximumEntryCount = 20_000;
    private const int UnixSymlinkMode = 0xA000;

    public static UpdatePackageManifest ExtractAndValidate(
        string archivePath,
        string extractionDirectory)
    {
        var archive = Path.GetFullPath(archivePath);
        var extraction = Path.GetFullPath(extractionDirectory);
        if (!File.Exists(archive))
        {
            throw new FileNotFoundException("The update archive was not found.", archive);
        }
        EnsureNotReparsePoint(archive);
        var archiveLength = new FileInfo(archive).Length;
        if (archiveLength <= 0 || archiveLength > MaxArchiveBytes)
        {
            throw new InvalidDataException("The update archive exceeds the size limit.");
        }
        if (Directory.Exists(extraction) || File.Exists(extraction))
        {
            throw new InvalidDataException("The update extraction directory must be new.");
        }

        Directory.CreateDirectory(extraction);
        try
        {
            EnsureNotReparsePoint(extraction);
            using var zip = ZipFile.OpenRead(archive);
            if (zip.Entries.Count is 0 or > MaximumEntryCount)
            {
                throw new InvalidDataException("The update archive file list is invalid.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long expandedBytes = 0;
            var copyBuffer = new byte[64 * 1024];
            foreach (var entry in zip.Entries)
            {
                var relative = NormalizeEntryName(entry.FullName);
                if (!names.Add(relative))
                {
                    throw new InvalidDataException("The update archive contains duplicate paths.");
                }
                if ((entry.ExternalAttributes >> 16 & 0xF000) == UnixSymlinkMode)
                {
                    throw new InvalidDataException("The update archive contains a symbolic link.");
                }
                if (!IsDirectoryEntry(entry))
                {
                    fileNames.Add(relative);
                }
            }

            foreach (var entry in zip.Entries)
            {
                var relative = NormalizeEntryName(entry.FullName);
                var destination = SafeCombine(extraction, relative);
                if (IsDirectoryEntry(entry))
                {
                    Directory.CreateDirectory(destination);
                    EnsureNotReparsePoint(destination);
                    continue;
                }

                var parent = Path.GetDirectoryName(destination)
                             ?? throw new InvalidDataException(
                                 "The update entry has no destination directory.");
                Directory.CreateDirectory(parent);
                EnsureNotReparsePoint(parent);
                if (entry.Length < 0 ||
                    entry.Length > MaxExpandedBytes - expandedBytes)
                {
                    throw new InvalidDataException(
                        "The expanded update exceeds the size limit.");
                }
                using var source = entry.Open();
                using var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    FileOptions.SequentialScan);
                long entryBytes = 0;
                while (true)
                {
                    var read = source.Read(copyBuffer, 0, copyBuffer.Length);
                    if (read == 0)
                    {
                        break;
                    }
                    if (entryBytes > MaxExpandedBytes - expandedBytes - read)
                    {
                        throw new InvalidDataException(
                            "The expanded update exceeds the size limit.");
                    }
                    entryBytes += read;
                    output.Write(copyBuffer, 0, read);
                }
                expandedBytes += entryBytes;
            }

            return ValidateStagedDirectory(extraction, fileNames);
        }
        catch
        {
            TryDeleteDirectory(extraction);
            throw;
        }
    }

    public static UpdatePackageManifest ValidateStagedDirectory(string stagingDirectory) =>
        ValidateStagedDirectory(
            Path.GetFullPath(stagingDirectory),
            expectedFiles: null);

    public static void VerifySha256(byte[] content, string expectedHash)
    {
        var normalized = NormalizeHash(expectedHash);
        var actual = SHA256.HashData(content);
        var expected = Convert.FromHexString(normalized);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new InvalidDataException("The update hash does not match.");
        }
    }

    public static string ReadSha256Text(byte[] content)
    {
        if (content.LongLength is 0 or > MaxHashBytes)
        {
            throw new InvalidDataException("The update hash file exceeds the size limit.");
        }
        var text = System.Text.Encoding.UTF8.GetString(content);
        var matches = System.Text.RegularExpressions.Regex.Matches(
            text,
            "(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            throw new InvalidDataException("The update hash file format is invalid.");
        }
        return matches[0].Value.ToLowerInvariant();
    }

    private static UpdatePackageManifest ValidateStagedDirectory(
        string stagingDirectory,
        HashSet<string>? expectedFiles)
    {
        if (!Directory.Exists(stagingDirectory))
        {
            throw new DirectoryNotFoundException("The update staging directory was not found.");
        }
        EnsureNotReparsePoint(stagingDirectory);

        var files = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(stagingDirectory);
        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                // Inspect the reparse bit before deciding whether to recurse. This
                // prevents a package or temp directory junction from redirecting
                // validation outside the staged tree.
                EnsureNotReparsePoint(entry);
                if (Directory.Exists(entry))
                {
                    pendingDirectories.Push(entry);
                    continue;
                }
                if (!File.Exists(entry))
                {
                    throw new InvalidDataException("The staged update contains an unknown entry.");
                }
                var relative = Path.GetRelativePath(stagingDirectory, entry)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                if (relative.StartsWith("../", StringComparison.Ordinal) ||
                    string.Equals(relative, "..", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The staged update contains an unsafe path.");
                }
                files.Add(relative);
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        if (expectedFiles is not null &&
            !expectedFiles
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(files, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The extracted update file list changed unexpectedly.");
        }
        var executable = files.FirstOrDefault(file =>
            string.Equals(file, "MyCO.exe", StringComparison.OrdinalIgnoreCase));
        if (executable is null)
        {
            throw new InvalidDataException("The update does not contain MyCO.exe.");
        }

        var executablePath = SafeCombine(stagingDirectory, executable);
        var executableHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(executablePath)))
            .ToLowerInvariant();
        return new UpdatePackageManifest(files, executablePath, executableHash);
    }

    private static string NormalizeEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\0'))
        {
            throw new InvalidDataException("The update archive contains an empty path.");
        }
        var normalized = name.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains(':'))
        {
            throw new InvalidDataException("The update archive contains an absolute path.");
        }
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException("The update archive contains a traversal path.");
        }
        return string.Join('/', segments);
    }

    private static string SafeCombine(string root, string relative)
    {
        var rootWithSeparator = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(root, relative));
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The update path escapes its destination.");
        }
        return combined;
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith('/') ||
        entry.FullName.EndsWith('\\');

    private static string NormalizeHash(string expectedHash)
    {
        var normalized = expectedHash.Trim();
        if (normalized.Length != 64 ||
            normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("The update hash format is invalid.");
        }
        return normalized;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The update contains a reparse point.");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // The caller still receives the validation failure; no update is applied.
        }
        catch (UnauthorizedAccessException)
        {
            // The caller still receives the validation failure; no update is applied.
        }
    }
}
