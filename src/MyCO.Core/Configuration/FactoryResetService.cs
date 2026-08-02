namespace MyCO.Configuration;

// Stages only MyCO-owned per-user state so a reset can commit or roll back atomically.
public sealed class FactoryResetService
{
    private const string StagingPrefix = ".myco-reset-";
    private readonly ConfigPaths _paths;

    public FactoryResetService(ConfigPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public FactoryResetTransaction Stage()
    {
        var root = Path.GetFullPath(_paths.BaseDirectory);
        Directory.CreateDirectory(root);
        ValidateTree(new DirectoryInfo(root));

        var targets = KnownTargets()
            .Select(path => new ResetTarget(path, Path.GetFileName(path)))
            .ToArray();
        foreach (var target in targets.Where(target => target.Exists))
        {
            var attributes = File.GetAttributes(target.SourcePath);
            ValidateKnownTarget(root, target.SourcePath, attributes);
            ValidateTree(
                Directory.Exists(target.SourcePath)
                    ? new DirectoryInfo(target.SourcePath)
                    : new FileInfo(target.SourcePath));
        }

        var staging = Path.Combine(root, $"{StagingPrefix}{Guid.NewGuid():N}");
        ValidateKnownTarget(root, staging, FileAttributes.Directory);
        Directory.CreateDirectory(staging);
        var staged = new List<ResetTarget>();
        try
        {
            foreach (var target in targets.Where(target => target.Exists))
            {
                var destination = Path.Combine(staging, target.Name);
                if (Directory.Exists(target.SourcePath))
                {
                    Directory.Move(target.SourcePath, destination);
                }
                else
                {
                    File.Move(target.SourcePath, destination);
                }
                staged.Add(target);
            }

            return new FactoryResetTransaction(root, staging, targets, staged);
        }
        catch
        {
            RestoreStaged(staging, staged);
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: false);
            }
            throw;
        }
    }

    internal static void ValidateKnownTarget(
        string baseDirectory,
        string target,
        FileAttributes attributes)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        var fullTarget = Path.GetFullPath(target);
        var parent = Directory.GetParent(fullTarget)?.FullName;
        if (!string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Factory reset target must be an immediate child of the MyCO data root.");
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Factory reset refuses reparse points.");
        }
    }

    internal static void ValidateTree(FileSystemInfo entry)
    {
        entry.Refresh();
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Factory reset refuses reparse points.");
        }
        if (entry is not DirectoryInfo directory)
        {
            return;
        }
        foreach (var child in directory.EnumerateFileSystemInfos())
        {
            ValidateTree(child);
        }
    }

    private IEnumerable<string> KnownTargets()
    {
        yield return _paths.ConfigFile;
        yield return _paths.CalibrationFile;
        yield return _paths.AvatarsDirectory;
        yield return _paths.LogsDirectory;
        yield return _paths.BackupsDirectory;
    }

    private static void RestoreStaged(string staging, IEnumerable<ResetTarget> targets)
    {
        foreach (var target in targets.Reverse())
        {
            var stagedPath = Path.Combine(staging, target.Name);
            if (Directory.Exists(stagedPath))
            {
                Directory.Move(stagedPath, target.SourcePath);
            }
            else if (File.Exists(stagedPath))
            {
                File.Move(stagedPath, target.SourcePath);
            }
        }
    }

    internal sealed record ResetTarget(string SourcePath, string Name)
    {
        public bool Exists =>
            File.Exists(SourcePath) || Directory.Exists(SourcePath);
    }
}

public sealed class FactoryResetTransaction : IDisposable
{
    private readonly string _root;
    private readonly string _staging;
    private readonly IReadOnlyList<FactoryResetService.ResetTarget> _targets;
    private readonly IReadOnlyList<FactoryResetService.ResetTarget> _staged;
    private bool _completed;

    internal FactoryResetTransaction(
        string root,
        string staging,
        IReadOnlyList<FactoryResetService.ResetTarget> targets,
        IReadOnlyList<FactoryResetService.ResetTarget> staged)
    {
        _root = root;
        _staging = staging;
        _targets = targets;
        _staged = staged;
    }

    public void Commit()
    {
        if (_completed)
        {
            return;
        }
        DeleteStaging();
        _completed = true;
    }

    public void Rollback()
    {
        if (_completed)
        {
            return;
        }

        foreach (var target in _targets)
        {
            DeleteKnownTarget(target.SourcePath);
        }
        foreach (var target in _staged)
        {
            var stagedPath = Path.Combine(_staging, target.Name);
            if (Directory.Exists(stagedPath))
            {
                Directory.Move(stagedPath, target.SourcePath);
            }
            else if (File.Exists(stagedPath))
            {
                File.Move(stagedPath, target.SourcePath);
            }
        }
        if (Directory.Exists(_staging))
        {
            Directory.Delete(_staging, recursive: false);
        }
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            Rollback();
        }
    }

    private void DeleteStaging()
    {
        if (!Directory.Exists(_staging))
        {
            return;
        }
        FactoryResetService.ValidateKnownTarget(
            _root,
            _staging,
            File.GetAttributes(_staging));
        FactoryResetService.ValidateTree(new DirectoryInfo(_staging));
        Directory.Delete(_staging, recursive: true);
    }

    private void DeleteKnownTarget(string target)
    {
        if (!File.Exists(target) && !Directory.Exists(target))
        {
            return;
        }
        FactoryResetService.ValidateKnownTarget(
            _root,
            target,
            File.GetAttributes(target));
        FactoryResetService.ValidateTree(
            Directory.Exists(target)
                ? new DirectoryInfo(target)
                : new FileInfo(target));
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
        else
        {
            File.Delete(target);
        }
    }
}
