using System.Diagnostics;
using System.Runtime.InteropServices;

// Restarts one verified desktop root, including the tray-only state that remains
// after Chromium closes its main window.
namespace MyCO.Applications;

public enum ApplicationCloseStatus
{
    Closed,
    StillRunning,
    IdentityUncertain,
    // The verified root exited on its own, but at least one unverified process
    // with the same executable path is still alive. This is the normal "child
    // outlived the parent" teardown case and is not a shutdown failure.
    StoppedButRemaining
}

public sealed record ApplicationProcessIdentity(
    int ProcessId,
    string ExecutablePath,
    DateTimeOffset StartedAt);

public sealed record ApplicationCloseAttempt(
    ApplicationCloseStatus Status,
    IReadOnlyList<ApplicationProcessIdentity> Targets)
{
    public bool IsClosed =>
        Status is ApplicationCloseStatus.Closed or
            ApplicationCloseStatus.StoppedButRemaining;
    public bool CanForceClose =>
        Status is ApplicationCloseStatus.StillRunning or
            ApplicationCloseStatus.StoppedButRemaining;
}

public enum ApplicationRestartStage
{
    IdentityValidation,
    GracefulClose,
    VerifiedForceClose,
    ProcessQuiescence
}

public sealed class ApplicationRestartException : InvalidOperationException
{
    public ApplicationRestartException(
        ApplicationRestartStage stage,
        Exception innerException)
        : base($"Desktop restart failed during {stage}.", innerException)
    {
        Stage = stage;
    }

    public ApplicationRestartStage Stage { get; }
}

public sealed record ApplicationRestartCloseResult(
    bool UsedVerifiedForceClose,
    IReadOnlyList<ApplicationProcessIdentity> Targets);

public sealed class ApplicationRestartService
{
    // Bounded tolerance for a same-name process that becomes temporarily
    // unreadable while it is tearing down. The root must still be verified by
    // the residual-count loop, so a recycled PID still fails closed.
    private const int MaxTransientSnapshotFailures = 8;
    private const int EntrySnapshotRetries = 5;

    private readonly IApplicationProcessBackend _backend;
    private readonly TimeSpan _trayDetectionGrace;
    private readonly TimeSpan _pollInterval;
    // Used only to avoid turning a transient read failure of a dying process
    // into a fabricated "the whole tree exited" signal.
    private IReadOnlyList<ApplicationProcessSnapshot> _lastGoodSnapshot = [];

    public ApplicationRestartService()
        : this(
            new WindowsApplicationProcessBackend(),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(250))
    {
    }

    internal ApplicationRestartService(
        IApplicationProcessBackend backend,
        TimeSpan? trayDetectionGrace = null,
        TimeSpan? pollInterval = null)
    {
        _backend = backend;
        _trayDetectionGrace = trayDetectionGrace ?? TimeSpan.FromSeconds(2);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);
    }

    public async Task<ApplicationRestartCloseResult> CloseForRestartAsync(
        ApplicationCandidate candidate,
        TimeSpan gracefulTimeout,
        TimeSpan forceTimeout,
        TimeSpan quiescenceTimeout,
        CancellationToken cancellationToken = default)
    {
        ApplicationCloseAttempt attempt;
        try
        {
            attempt = await RequestGracefulCloseAsync(
                candidate,
                gracefulTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ApplicationRestartException(
                ApplicationRestartStage.IdentityValidation,
                exception);
        }

        if (attempt.Status == ApplicationCloseStatus.Closed)
        {
            // Nothing was running; no force and no quiescence wait.
            return new ApplicationRestartCloseResult(
                false,
                attempt.Targets);
        }

        if (attempt.Status == ApplicationCloseStatus.StoppedButRemaining)
        {
            // The verified root exited on its own. Clean any verified residual
            // descendants so a stale singleton cannot block the new instance.
            // Only verified headless descendants are touched, never a windowed
            // or unattributed process.
            await ForceCloseAsync(
                candidate,
                attempt,
                forceTimeout,
                cancellationToken).ConfigureAwait(false);
            return new ApplicationRestartCloseResult(
                true,
                attempt.Targets);
        }

        var usedForce = false;
        if (!attempt.CanForceClose)
        {
            throw new ApplicationRestartException(
                ApplicationRestartStage.IdentityValidation,
                new InvalidOperationException(
                    "The Desktop restart target is not safe to terminate."));
        }
        try
        {
            await ForceCloseAsync(
                candidate,
                attempt,
                forceTimeout,
                cancellationToken).ConfigureAwait(false);
            usedForce = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ApplicationRestartException(
                ApplicationRestartStage.VerifiedForceClose,
                exception);
        }

        try
        {
            await WaitForQuiescenceAsync(
                candidate,
                quiescenceTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ApplicationRestartException(
                ApplicationRestartStage.ProcessQuiescence,
                exception);
        }

        return new ApplicationRestartCloseResult(
            usedForce,
            attempt.Targets);
    }

    public async Task<ApplicationCloseAttempt> RequestGracefulCloseAsync(
        ApplicationCandidate candidate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var matching = SnapshotWithEntryRetry(candidate);
        if (matching.Count == 0)
        {
            return new ApplicationCloseAttempt(
                ApplicationCloseStatus.Closed,
                []);
        }

        var roots = SelectRoots(matching);
        if (roots.Count != 1)
        {
            throw new InvalidOperationException(
                roots.Count == 0
                    ? "The running Desktop root process could not be identified safely."
                    : "Multiple Desktop root processes match the selected installation.");
        }

        var target = roots[0].Identity;
        var closeAttempt = new ApplicationCloseAttempt(
            ApplicationCloseStatus.StillRunning,
            [target]);
        if (roots[0].HasMainWindow)
        {
            if (_backend.RequestClose(target))
            {
                // Graceful close has begun; the tree may exit over many seconds.
                return await WaitForShutdownOrShrinkAsync(
                    candidate,
                    target,
                    timeout,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        // No WM_CLOSE was sent (tray-only root, or the window refused the close).
        return closeAttempt;
    }

    // True when at least one verified root of the installation is still running.
    // Used as a final pre-launch guard after a close stage that was tolerated.
    public bool IsRootRunning(ApplicationCandidate candidate)
    {
        var matching = MatchingProcesses(candidate);
        return SelectRoots(matching).Count > 0;
    }

    public async Task ForceCloseAsync(
        ApplicationCandidate candidate,
        ApplicationCloseAttempt attempt,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!attempt.CanForceClose)
        {
            throw new InvalidOperationException(
                "The Desktop restart target is not safe to terminate.");
        }

        var current = MatchingProcesses(candidate);
        var tracked = AttemptedCloseTargets(attempt, current);

        // Close any residual path-matching process that is not an attempted
        // target only when it is a verified descendant of a tracked member.
        // An untracked process with a main window is treated as another root
        // and fails closed instead of being terminated.
        foreach (var identity in CloseableResiduals(attempt, current))
        {
            TryKill(candidate, identity);
        }

        foreach (var target in tracked)
        {
            TryKill(candidate, target);
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        var transientFailures = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = SnapshotForPoll(candidate, ref transientFailures);
            if (ResidualCount(candidate, attempt, current) == 0)
            {
                return;
            }
            // Fail closed on a recycled PID or an identity change of any target.
            foreach (var target in attempt.Targets)
            {
                switch (IdentityState(target, current))
                {
                    case ApplicationProcessState.Running:
                        // An explicit Kill is retried for a stubborn verified target.
                        TryKill(candidate, target);
                        break;
                    case ApplicationProcessState.Uncertain:
                        throw new InvalidOperationException(
                            "The Desktop restart target identity changed during termination.");
                }
            }
            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The selected Desktop process tree did not exit before restart.");
    }

    public async Task WaitForQuiescenceAsync(
        ApplicationCandidate candidate,
        TimeSpan timeout,
        int requiredStableSamples = 3,
        CancellationToken cancellationToken = default)
    {
        if (requiredStableSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredStableSamples));
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        var stableSamples = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matching = MatchingProcesses(candidate);
            if (matching.Count == 0)
            {
                stableSamples++;
                if (stableSamples >= requiredStableSamples)
                {
                    return;
                }
            }
            else
            {
                // A late child or singleton hand-off resets the stability window.
                stableSamples = 0;
            }
            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The selected Desktop installation did not reach a stable stopped state.");
    }

    // Waits for the verified tree to exit on its own after a successful WM_CLOSE.
    // A windowless root is not a failure: a closing Chromium tree can stay
    // windowless for seconds while its browser process is the last to exit.
    // Only the graceful timeout decides that the tree truly refuses to exit.
    private async Task<ApplicationCloseAttempt> WaitForShutdownOrShrinkAsync(
        ApplicationCandidate candidate,
        ApplicationProcessIdentity target,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var tracked = new Dictionary<int, ApplicationProcessIdentity>
        {
            [target.ProcessId] = target
        };
        var deadline = DateTimeOffset.UtcNow + timeout;
        var transientFailures = 0;
        _lastGoodSnapshot = SnapshotWithEntryRetry(candidate);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = SnapshotForPoll(candidate, ref transientFailures);
            var trackedNow = current
                .Where(process => tracked.ContainsKey(process.ProcessId))
                .ToArray();

            // The window closed and the tracked tree exited. Remaining same-name
            // processes are unverified teardown stragglers, not a shutdown failure.
            // Targets carry the original verified root so the caller can clean
            // only its verified descendants without touching any windowed process.
            if (trackedNow.Length == 0)
            {
                return new ApplicationCloseAttempt(
                    ApplicationCloseStatus.StoppedButRemaining,
                    [target]);
            }

            // A tracked process lost its identity, or a new untracked process
            // with a main window appeared. Both fail closed.
            foreach (var process in trackedNow)
            {
                if (IdentityState(tracked[process.ProcessId], current) ==
                    ApplicationProcessState.Uncertain)
                {
                    return new ApplicationCloseAttempt(
                        ApplicationCloseStatus.IdentityUncertain,
                        [target]);
                }
            }

            // Always grow the tracked set with same-path, main-window-less
            // processes; a verified close target can re-spawn children while it exits.
            GrowTracked(candidate, current, tracked);

            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        // The verified tree is still alive after the graceful window.
        return new ApplicationCloseAttempt(
            ApplicationCloseStatus.StillRunning,
            tracked.Values.ToArray());
    }

    // A snapshot taken during close can transiently fail to read a dying process.
    // Absorb a bounded number of failures and keep polling with the last good
    // snapshot; a persistent failure still propagates so the caller can fail closed.
    private IReadOnlyList<ApplicationProcessSnapshot> SnapshotForPoll(
        ApplicationCandidate candidate,
        ref int transientFailures)
    {
        try
        {
            _lastGoodSnapshot = MatchingProcesses(candidate);
            transientFailures = 0;
            return _lastGoodSnapshot;
        }
        catch (InvalidOperationException) when (transientFailures < MaxTransientSnapshotFailures)
        {
            transientFailures++;
            return _lastGoodSnapshot;
        }
    }

    private IReadOnlyList<ApplicationProcessSnapshot> SnapshotWithEntryRetry(
        ApplicationCandidate candidate)
    {
        for (var attempt = 0; attempt < EntrySnapshotRetries; attempt++)
        {
            try
            {
                return MatchingProcesses(candidate);
            }
            catch (InvalidOperationException) when (attempt < EntrySnapshotRetries - 1)
            {
                // A partially closed tree can be unreadable for a few hundred
                // milliseconds; retry before classifying this as unsafe.
                Thread.Sleep(50);
            }
        }
        return MatchingProcesses(candidate);
    }

    private void GrowTracked(
        ApplicationCandidate candidate,
        IReadOnlyList<ApplicationProcessSnapshot> current,
        Dictionary<int, ApplicationProcessIdentity> tracked)
    {
        foreach (var process in current)
        {
            if (tracked.ContainsKey(process.ProcessId) || process.HasMainWindow)
            {
                continue;
            }
            if (IsVerifiedDescendant(process, tracked.Keys))
            {
                tracked[process.ProcessId] = process.Identity;
            }
        }
    }

    private IReadOnlyList<ApplicationProcessIdentity> AttemptedCloseTargets(
        ApplicationCloseAttempt attempt,
        IReadOnlyList<ApplicationProcessSnapshot> current)
    {
        return attempt.Targets
            .Where(target => IdentityState(target, current) ==
                             ApplicationProcessState.Running)
            .ToArray();
    }

    // Residuals are forced only when they are verified descendants of a tracked
    // target. A windowed process that was never part of the verified tree is a
    // separate root and must fail closed instead of being terminated.
    private IReadOnlyList<ApplicationProcessIdentity> CloseableResiduals(
        ApplicationCloseAttempt attempt,
        IReadOnlyList<ApplicationProcessSnapshot> current)
    {
        var trackedIds = attempt.Targets.Select(target => target.ProcessId).ToHashSet();
        return current
            .Where(process =>
                !trackedIds.Contains(process.ProcessId) &&
                !process.HasMainWindow &&
                IsVerifiedDescendant(process, trackedIds))
            .Select(process => process.Identity)
            .ToArray();
    }

    private bool IsVerifiedDescendant(
        ApplicationProcessSnapshot process,
        IEnumerable<int> trackedIds) =>
        IsVerifiedDescendant(_backend, process, trackedIds);

    private static bool IsVerifiedDescendant(
        IApplicationProcessBackend backend,
        ApplicationProcessSnapshot process,
        IEnumerable<int> trackedIds)
    {
        var visited = new HashSet<int> { process.ProcessId };
        var parentId = process.ParentProcessId;
        while (parentId > 0)
        {
            if (trackedIds.Contains(parentId))
            {
                return true;
            }
            if (!visited.Add(parentId))
            {
                return false;
            }
            parentId = backend.ParentProcessId(parentId);
        }
        return false;
    }

    private int ResidualCount(
        ApplicationCandidate candidate,
        ApplicationCloseAttempt attempt,
        IReadOnlyList<ApplicationProcessSnapshot> current)
    {
        var trackedIds = attempt.Targets.Select(target => target.ProcessId).ToHashSet();
        var count = 0;
        foreach (var process in current)
        {
            if (trackedIds.Contains(process.ProcessId))
            {
                count++;
                continue;
            }
            if (process.HasMainWindow ||
                !IsVerifiedDescendant(process, trackedIds))
            {
                continue;
            }
            count++;
        }
        return count;
    }

    // A kill can race a normally exiting process. Both the invalid-op case (the
    // process no longer matches its captured identity) and the Win32 case (the
    // process exited between identity validation and Kill) are absorbed here;
    // the residual-count loop remains the authoritative verifier and a recycled
    // PID still fails closed through IdentityState.
    private void TryKill(ApplicationCandidate candidate, ApplicationProcessIdentity identity)
    {
        try
        {
            _backend.KillTree(identity);
        }
        catch (InvalidOperationException)
        {
            // The target no longer matches its captured identity; refusing to
            // kill is the safe action. The poll loop decides whether it exited.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The process exited between the identity check and Kill. That is
            // the successful outcome of the shutdown, not a shutdown failure.
        }
    }

    internal static IReadOnlyList<ApplicationProcessSnapshot> SelectRoots(
        IReadOnlyList<ApplicationProcessSnapshot> matching)
    {
        var matchingById = matching.ToDictionary(
            process => process.ProcessId);
        return matching
            .Where(process =>
                !matchingById.TryGetValue(
                    process.ParentProcessId,
                    out var possibleParent) ||
                possibleParent.StartedAt > process.StartedAt)
            .OrderBy(process => process.StartedAt)
            .ToArray();
    }

    private IReadOnlyList<ApplicationProcessSnapshot> MatchingProcesses(
        ApplicationCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.ExecutablePath))
        {
            return [];
        }

        var expectedPath = Path.GetFullPath(candidate.ExecutablePath);
        return _backend.Snapshot(candidate.ProcessName)
            .Where(process => process.ExecutablePath.Equals(
                expectedPath,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static ApplicationProcessState IdentityState(
        ApplicationProcessIdentity identity,
        IReadOnlyList<ApplicationProcessSnapshot> current)
    {
        var samePid = current.FirstOrDefault(
            process => process.ProcessId == identity.ProcessId);
        if (samePid is null)
        {
            return ApplicationProcessState.Exited;
        }
        return samePid.ExecutablePath.Equals(
                   identity.ExecutablePath,
                   StringComparison.OrdinalIgnoreCase) &&
               samePid.StartedAt == identity.StartedAt
            ? ApplicationProcessState.Running
            : ApplicationProcessState.Uncertain;
    }
}

internal enum ApplicationProcessState
{
    Exited,
    Running,
    Uncertain
}

internal sealed record ApplicationProcessSnapshot(
    int ProcessId,
    int ParentProcessId,
    string ExecutablePath,
    DateTimeOffset StartedAt,
    bool HasMainWindow)
{
    public ApplicationProcessIdentity Identity =>
        new(ProcessId, ExecutablePath, StartedAt);
}

internal interface IApplicationProcessBackend
{
    IReadOnlyList<ApplicationProcessSnapshot> Snapshot(string processName);
    bool RequestClose(ApplicationProcessIdentity identity);
    void KillTree(ApplicationProcessIdentity identity);
    int ParentProcessId(int processId);
}

internal sealed class WindowsApplicationProcessBackend : IApplicationProcessBackend
{
    private static readonly Dictionary<int, int> EmptyParents =
        [];

    public IReadOnlyList<ApplicationProcessSnapshot> Snapshot(string processName)
    {
        var parentIds = ParentProcessIds();
        var snapshots = new List<ApplicationProcessSnapshot>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }
                    snapshots.Add(new ApplicationProcessSnapshot(
                        process.Id,
                        parentIds.GetValueOrDefault(process.Id),
                        Path.GetFullPath(path),
                        process.StartTime.ToUniversalTime(),
                        process.MainWindowHandle != IntPtr.Zero));
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or
                        System.ComponentModel.Win32Exception or
                        NotSupportedException)
                {
                    // Any same-name process with an unreadable identity could
                    // be another matching root. Skipping it would turn
                    // uncertainty into permission to close a different root.
                    throw new InvalidOperationException(
                        "A Desktop process identity could not be read safely.",
                        exception);
                }
            }
        }
        return snapshots;
    }

    public bool RequestClose(ApplicationProcessIdentity identity)
    {
        using var process = OpenVerified(identity);
        return process is not null && process.CloseMainWindow();
    }

    public void KillTree(ApplicationProcessIdentity identity)
    {
        using var process = OpenVerified(identity)
                            ?? throw new InvalidOperationException(
                                "The Desktop restart target no longer matches its identity.");
        process.Kill(entireProcessTree: true);
    }

    public int ParentProcessId(int processId)
    {
        var parentIds = ParentProcessIds();
        return parentIds.GetValueOrDefault(processId);
    }

    private static Process? OpenVerified(ApplicationProcessIdentity identity)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(identity.ProcessId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        try
        {
            var path = process.MainModule?.FileName;
            var startedAt = process.StartTime.ToUniversalTime();
            if (path?.Equals(
                    identity.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase) == true &&
                startedAt == identity.StartedAt)
            {
                return process;
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                NotSupportedException)
        {
            // Fall through to fail closed.
        }

        process.Dispose();
        return null;
    }

    private static IReadOnlyDictionary<int, int> ParentProcessIds()
    {
        var result = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot == InvalidHandleValue)
        {
            return EmptyParents;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
            };
            if (!Process32First(snapshot, ref entry))
            {
                return EmptyParents;
            }
            do
            {
                result[(int)entry.ProcessId] = (int)entry.ParentProcessId;
                entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
            }
            while (Process32Next(snapshot, ref entry));
            return result;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private const uint SnapshotProcesses = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint UsageCount;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "Process32FirstW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint snapshot, ref ProcessEntry32 entry);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "Process32NextW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
