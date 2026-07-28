using System.Diagnostics;
using System.Runtime.InteropServices;

// Restarts one verified desktop root, including the tray-only state that remains
// after Chromium closes its main window.
namespace MyCodex.Applications;

public enum ApplicationCloseStatus
{
    Closed,
    StillRunning,
    IdentityUncertain
}

public sealed record ApplicationProcessIdentity(
    int ProcessId,
    string ExecutablePath,
    DateTimeOffset StartedAt);

public sealed record ApplicationCloseAttempt(
    ApplicationCloseStatus Status,
    IReadOnlyList<ApplicationProcessIdentity> Targets)
{
    public bool IsClosed => Status == ApplicationCloseStatus.Closed;
    public bool CanForceClose =>
        Status == ApplicationCloseStatus.StillRunning && Targets.Count > 0;
}

public sealed class ApplicationRestartService
{
    private readonly IApplicationProcessBackend _backend;
    private readonly TimeSpan _trayDetectionGrace;
    private readonly TimeSpan _pollInterval;

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

    public async Task<ApplicationCloseAttempt> RequestGracefulCloseAsync(
        ApplicationCandidate candidate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var matching = MatchingProcesses(candidate);
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
        if (roots[0].HasMainWindow)
        {
            _backend.RequestClose(target);
        }
        else
        {
            // A pre-existing tray-only root cannot receive WM_CLOSE; offer the
            // verified force-restart path immediately instead of reporting success.
            return new ApplicationCloseAttempt(
                ApplicationCloseStatus.StillRunning,
                [target]);
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        DateTimeOffset? windowlessSince = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = MatchingProcesses(candidate);
            if (current.Count == 0)
            {
                return new ApplicationCloseAttempt(
                    ApplicationCloseStatus.Closed,
                    [target]);
            }

            var identityState = IdentityState(target, current);
            if (identityState == ApplicationProcessState.Uncertain)
            {
                return new ApplicationCloseAttempt(
                    ApplicationCloseStatus.IdentityUncertain,
                    [target]);
            }

            var targetSnapshot = current.FirstOrDefault(
                process => process.ProcessId == target.ProcessId);
            if (targetSnapshot is not null && !targetSnapshot.HasMainWindow)
            {
                windowlessSince ??= DateTimeOffset.UtcNow;
                if (DateTimeOffset.UtcNow - windowlessSince >= _trayDetectionGrace)
                {
                    return new ApplicationCloseAttempt(
                        ApplicationCloseStatus.StillRunning,
                        [target]);
                }
            }
            else
            {
                windowlessSince = null;
            }

            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        return new ApplicationCloseAttempt(
            ApplicationCloseStatus.StillRunning,
            [target]);
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
        var runningTargets = new List<ApplicationProcessIdentity>();
        foreach (var target in attempt.Targets)
        {
            switch (IdentityState(target, current))
            {
                case ApplicationProcessState.Running:
                    runningTargets.Add(target);
                    break;
                case ApplicationProcessState.Uncertain:
                    throw new InvalidOperationException(
                        "The Desktop restart target identity changed before termination.");
            }
        }

        // Validate every target before terminating any of them.
        foreach (var target in runningTargets)
        {
            try
            {
                _backend.KillTree(target);
            }
            catch (InvalidOperationException)
            {
                // A normally exiting process can disappear between validation
                // and Kill. Accept only a confirmed exit; PID reuse still fails closed.
                current = MatchingProcesses(candidate);
                if (IdentityState(target, current) != ApplicationProcessState.Exited)
                {
                    throw;
                }
            }
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = MatchingProcesses(candidate);
            if (current.Count == 0)
            {
                return;
            }
            foreach (var target in attempt.Targets)
            {
                if (IdentityState(target, current) == ApplicationProcessState.Uncertain)
                {
                    throw new InvalidOperationException(
                        "The Desktop restart target identity changed during termination.");
                }
            }
            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The selected Desktop process tree did not exit before restart.");
    }

    internal static IReadOnlyList<ApplicationProcessSnapshot> SelectRoots(
        IReadOnlyList<ApplicationProcessSnapshot> matching)
    {
        var matchingIds = matching
            .Select(process => process.ProcessId)
            .ToHashSet();
        return matching
            .Where(process => !matchingIds.Contains(process.ParentProcessId))
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
}

internal sealed class WindowsApplicationProcessBackend : IApplicationProcessBackend
{
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
                    // An unreadable process cannot become a restart target.
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
            return result;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
            };
            if (!Process32First(snapshot, ref entry))
            {
                return result;
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
