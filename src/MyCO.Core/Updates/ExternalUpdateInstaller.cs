using System.Diagnostics;
using System.Security.Cryptography;

namespace MyCO.Updates;

public sealed record ProcessIdentity(
    int ProcessId,
    string ExecutablePath,
    long StartTimeUtcTicks,
    bool IsRunning);

public sealed record UpdateApplyRequest(
    int ProcessId,
    string CurrentProcessPath,
    long CurrentProcessStartTimeUtcTicks,
    string InstallDirectory,
    string StagedDirectory,
    string ExpectedExecutableSha256,
    bool LaunchNewProcess = true,
    TimeSpan? WaitTimeout = null,
    string? CleanupDirectory = null);

public sealed class UpdateInstallException : Exception
{
    public UpdateInstallException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

internal interface IUpdateProcessProbe
{
    ProcessIdentity? Get(int processId);
}

public sealed class ExternalUpdateInstaller
{
    private readonly IUpdateProcessProbe _processProbe;
    private readonly Action<string> _launch;

    public ExternalUpdateInstaller()
        : this(new SystemUpdateProcessProbe(), Launch)
    {
    }

    internal ExternalUpdateInstaller(
        IUpdateProcessProbe processProbe,
        Action<string> launch)
    {
        _processProbe = processProbe;
        _launch = launch;
    }

    public async Task ApplyAsync(
        UpdateApplyRequest request,
        CancellationToken cancellationToken)
    {
        var install = Path.GetFullPath(request.InstallDirectory);
        var stage = Path.GetFullPath(request.StagedDirectory);
        var currentExecutable = Path.GetFullPath(request.CurrentProcessPath);
        var cleanup = ValidateCleanupDirectory(request.CleanupDirectory);
        var expectedHash = NormalizeHash(request.ExpectedExecutableSha256);
        if (!Directory.Exists(install) ||
            !Directory.Exists(stage) ||
            !File.Exists(currentExecutable) ||
            !IsSameDirectory(
                Path.GetDirectoryName(currentExecutable)!,
                install) ||
            IsSameDirectory(install, stage) ||
            IsSameDirectory(install, cleanup) ||
            IsWithin(stage, install) ||
            IsWithin(install, stage) ||
            !IsWithin(stage, cleanup) ||
            IsSameDirectory(stage, cleanup) ||
            IsWithin(install, cleanup) ||
            IsWithin(cleanup, install))
        {
            throw new UpdateInstallException("The update paths are not valid.");
        }

        UpdatePackageManifest stagedManifest;
        try
        {
            stagedManifest = UpdatePackageValidator.ValidateStagedDirectory(stage);
            UpdatePackageValidator.VerifySha256(
                File.ReadAllBytes(stagedManifest.ExecutablePath),
                expectedHash);
            EnsureNoReparsePoint(install);
            EnsureNoReparsePoint(currentExecutable);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                InvalidDataException or ArgumentException)
        {
            throw new UpdateInstallException(
                "The staged update did not pass validation.",
                exception);
        }

        var backup = $"{install}.backup-{Guid.NewGuid():N}";
        var failed = $"{install}.failed-{Guid.NewGuid():N}";
        var oldMoved = false;
        var newMoved = false;
        try
        {
            await WaitForExactProcessExitAsync(request, cancellationToken)
                .ConfigureAwait(false);
            Directory.Move(install, backup);
            oldMoved = true;
            Directory.Move(stage, install);
            newMoved = true;

            var installedExecutable = Path.Combine(install, "MyCO.exe");
            if (!File.Exists(installedExecutable))
            {
                throw new UpdateInstallException(
                    "The replacement does not contain MyCO.exe.");
            }
            EnsureNoReparsePoint(install);
            UpdatePackageValidator.VerifySha256(
                File.ReadAllBytes(installedExecutable),
                expectedHash);

            if (request.LaunchNewProcess)
            {
                _launch(installedExecutable);
            }
            TryDeleteDirectory(backup);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UpdateInstallException exception)
        {
            if (!oldMoved && !newMoved)
            {
                throw;
            }
            RollBack(
                install,
                backup,
                failed,
                oldMoved,
                newMoved,
                exception);
        }
        catch (Exception exception)
        {
            if (!oldMoved && !newMoved)
            {
                throw new UpdateInstallException(
                    "The update could not begin replacing the installation.",
                    exception);
            }
            RollBack(
                install,
                backup,
                failed,
                oldMoved,
                newMoved,
                exception);
        }
        finally
        {
            TryDeleteDirectory(stage);
            TryDeleteDirectory(failed);
            TryDeleteDirectory(backup);
            TryDeleteDirectory(cleanup);
        }
    }

    private async Task WaitForExactProcessExitAsync(
        UpdateApplyRequest request,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow +
                       (request.WaitTimeout ?? TimeSpan.FromMinutes(5));
        var observed = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = _processProbe.Get(request.ProcessId);
            if (identity is null)
            {
                if (observed)
                {
                    return;
                }
                throw new UpdateInstallException(
                    "The process identity could not be verified before replacement.");
            }
            if (!string.Equals(
                    Path.GetFullPath(identity.ExecutablePath),
                    Path.GetFullPath(request.CurrentProcessPath),
                    StringComparison.OrdinalIgnoreCase) ||
                identity.StartTimeUtcTicks != request.CurrentProcessStartTimeUtcTicks)
            {
                throw new UpdateInstallException(
                    "The process identity changed; the update was stopped.");
            }
            observed = true;
            if (!identity.IsRunning)
            {
                return;
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new UpdateInstallException(
                    "MyCO did not exit within the safe update window.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void RollBack(
        string install,
        string backup,
        string failed,
        bool oldMoved,
        bool newMoved,
        Exception cause)
    {
        try
        {
            if (newMoved && Directory.Exists(install))
            {
                Directory.Move(install, failed);
            }
            if (oldMoved && Directory.Exists(backup) && !Directory.Exists(install))
            {
                Directory.Move(backup, install);
            }
        }
        catch (Exception rollbackException)
        {
            throw new UpdateInstallException(
                "The update failed and automatic rollback could not be completed.",
                new AggregateException(cause, rollbackException));
        }
        throw new UpdateInstallException(
            "The update failed; the previous installation was restored.",
            cause);
    }

    private static void Launch(string executablePath)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            UseShellExecute = true
        });
        if (process is null)
        {
            throw new InvalidOperationException("The updated MyCO process could not be started.");
        }
    }

    private static string NormalizeHash(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new UpdateInstallException("The expected update hash is invalid.");
        }
        return normalized;
    }

    private static bool IsWithin(string path, string parent)
    {
        var root = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(
            root,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameDirectory(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string ValidateCleanupDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UpdateInstallException(
                "The update cleanup directory is missing.");
        }

        var cleanup = Path.GetFullPath(value);
        var updatesRoot = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "MyCO", "Updates"));
        if (!Directory.Exists(updatesRoot))
        {
            throw new UpdateInstallException(
                "The private update area is unavailable.");
        }
        EnsureNoReparsePoint(updatesRoot);
        if (IsSameDirectory(cleanup, updatesRoot) ||
            !IsWithin(cleanup, updatesRoot))
        {
            throw new UpdateInstallException(
                "The update cleanup directory is outside the private update area.");
        }
        if (Directory.Exists(cleanup))
        {
            EnsureNoReparsePoint(cleanup);
        }
        return cleanup;
    }

    private static void EnsureNoReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The update path is a reparse point.");
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class SystemUpdateProcessProbe : IUpdateProcessProbe
    {
        public ProcessIdentity? Get(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var path = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }
                var start = process.StartTime.ToUniversalTime().Ticks;
                return new ProcessIdentity(processId, path, start, !process.HasExited);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException exception)
            {
                throw new UpdateInstallException(
                    "The target process identity could not be verified.",
                    exception);
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                throw new UpdateInstallException(
                    "The target process identity could not be verified.",
                    exception);
            }
        }
    }
}
