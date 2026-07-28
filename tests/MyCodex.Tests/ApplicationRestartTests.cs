using MyCodex.Applications;

namespace MyCodex.Tests;

public sealed class ApplicationRestartTests
{
    private const string ExecutablePath =
        @"C:\Program Files\OpenAI\ChatGPT.exe";

    [Fact]
    public async Task TrayOnlyRootIsNotMistakenForAClosedApplication()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var backend = new FakeProcessBackend(
            new ApplicationProcessSnapshot(
                100,
                10,
                ExecutablePath,
                startedAt,
                HasMainWindow: false),
            new ApplicationProcessSnapshot(
                101,
                100,
                ExecutablePath,
                startedAt.AddSeconds(1),
                HasMainWindow: false));
        var service = new ApplicationRestartService(backend);

        var attempt = await service.RequestGracefulCloseAsync(
            Candidate(),
            TimeSpan.FromSeconds(1));

        Assert.Equal(ApplicationCloseStatus.StillRunning, attempt.Status);
        Assert.True(attempt.CanForceClose);
        Assert.Single(attempt.Targets);
        Assert.Equal(100, attempt.Targets[0].ProcessId);
        Assert.Empty(backend.CloseRequests);
    }

    [Fact]
    public async Task ForceCloseUsesCapturedIdentityAndWaitsForTheTreeToExit()
    {
        var backend = new FakeProcessBackend(
            new ApplicationProcessSnapshot(
                200,
                20,
                ExecutablePath,
                DateTimeOffset.UtcNow.AddMinutes(-2),
                HasMainWindow: false));
        backend.OnKill = _ => backend.Snapshots.Clear();
        var service = new ApplicationRestartService(backend);
        var attempt = await service.RequestGracefulCloseAsync(
            Candidate(),
            TimeSpan.FromSeconds(1));

        await service.ForceCloseAsync(
            Candidate(),
            attempt,
            TimeSpan.FromSeconds(1));

        var killed = Assert.Single(backend.KillRequests);
        Assert.Equal(attempt.Targets[0], killed);
        Assert.Empty(backend.Snapshots);
    }

    [Fact]
    public async Task WindowClosingIntoTrayUsesVerifiedForceRestartPath()
    {
        var root = new ApplicationProcessSnapshot(
            250,
            25,
            ExecutablePath,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            HasMainWindow: true);
        var backend = new FakeProcessBackend(root);
        backend.OnClose = _ =>
        {
            backend.Snapshots[0] = root with { HasMainWindow = false };
            return true;
        };
        backend.OnKill = _ => backend.Snapshots.Clear();
        var service = new ApplicationRestartService(
            backend,
            trayDetectionGrace: TimeSpan.FromMilliseconds(10),
            pollInterval: TimeSpan.FromMilliseconds(2));

        var attempt = await service.RequestGracefulCloseAsync(
            Candidate(),
            TimeSpan.FromSeconds(1));

        Assert.Equal(ApplicationCloseStatus.StillRunning, attempt.Status);
        Assert.Equal(250, Assert.Single(attempt.Targets).ProcessId);
        await service.ForceCloseAsync(
            Candidate(),
            attempt,
            TimeSpan.FromSeconds(1));
        Assert.Equal(250, Assert.Single(backend.KillRequests).ProcessId);
        Assert.Empty(backend.Snapshots);
    }

    [Fact]
    public async Task PidReuseFailsClosedWithoutTerminatingTheReplacement()
    {
        var initial = new ApplicationProcessSnapshot(
            300,
            30,
            ExecutablePath,
            DateTimeOffset.UtcNow.AddMinutes(-3),
            HasMainWindow: false);
        var backend = new FakeProcessBackend(initial);
        var service = new ApplicationRestartService(backend);
        var attempt = await service.RequestGracefulCloseAsync(
            Candidate(),
            TimeSpan.FromSeconds(1));
        backend.Snapshots[0] = initial with
        {
            StartedAt = initial.StartedAt.AddMinutes(2)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ForceCloseAsync(
                Candidate(),
                attempt,
                TimeSpan.FromSeconds(1)));

        Assert.Empty(backend.KillRequests);
    }

    [Fact]
    public async Task NaturalExitRacingForceCloseIsAcceptedWithoutKillingAnotherProcess()
    {
        var backend = new FakeProcessBackend(
            new ApplicationProcessSnapshot(
                350,
                35,
                ExecutablePath,
                DateTimeOffset.UtcNow.AddMinutes(-3),
                HasMainWindow: false));
        backend.OnKill = _ =>
        {
            backend.Snapshots.Clear();
            throw new InvalidOperationException("Process already exited.");
        };
        var service = new ApplicationRestartService(backend);
        var attempt = await service.RequestGracefulCloseAsync(
            Candidate(),
            TimeSpan.FromSeconds(1));

        await service.ForceCloseAsync(
            Candidate(),
            attempt,
            TimeSpan.FromSeconds(1));

        Assert.Empty(backend.Snapshots);
        Assert.Single(backend.KillRequests);
    }

    [Fact]
    public async Task MultipleMatchingRootsFailClosed()
    {
        var backend = new FakeProcessBackend(
            new ApplicationProcessSnapshot(
                400,
                40,
                ExecutablePath,
                DateTimeOffset.UtcNow.AddMinutes(-4),
                HasMainWindow: true),
            new ApplicationProcessSnapshot(
                500,
                50,
                ExecutablePath,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                HasMainWindow: true));
        var service = new ApplicationRestartService(backend);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestGracefulCloseAsync(
                Candidate(),
                TimeSpan.FromSeconds(1)));

        Assert.Empty(backend.CloseRequests);
        Assert.Empty(backend.KillRequests);
    }

    [Fact]
    public async Task RecycledParentPidCannotHideASecondRoot()
    {
        var recycledParent = new ApplicationProcessSnapshot(
            450,
            45,
            ExecutablePath,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            HasMainWindow: true);
        var olderIndependentRoot = new ApplicationProcessSnapshot(
            550,
            450,
            ExecutablePath,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            HasMainWindow: true);
        var backend = new FakeProcessBackend(
            recycledParent,
            olderIndependentRoot);
        var service = new ApplicationRestartService(backend);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestGracefulCloseAsync(
                Candidate(),
                TimeSpan.FromSeconds(1)));

        Assert.Empty(backend.CloseRequests);
        Assert.Empty(backend.KillRequests);
    }

    [Fact]
    public async Task UnreadableSameNameIdentityFailsClosed()
    {
        var backend = new FakeProcessBackend
        {
            SnapshotException = new InvalidOperationException(
                "Identity unreadable.")
        };
        var service = new ApplicationRestartService(backend);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestGracefulCloseAsync(
                Candidate(),
                TimeSpan.FromSeconds(1)));

        Assert.Empty(backend.CloseRequests);
        Assert.Empty(backend.KillRequests);
    }

    [Fact]
    public async Task NormalWindowCloseStillCompletesWithoutForce()
    {
        var backend = new FakeProcessBackend(
            new ApplicationProcessSnapshot(
                600,
                60,
                ExecutablePath,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                HasMainWindow: true));
        backend.OnClose = _ =>
        {
            backend.Snapshots.Clear();
            return true;
        };
        var service = new ApplicationRestartService(backend);

        var attempt = await service.RequestGracefulCloseAsync(
            Candidate(),
            TimeSpan.FromSeconds(1));

        Assert.True(attempt.IsClosed);
        Assert.Single(backend.CloseRequests);
        Assert.Empty(backend.KillRequests);
    }

    [Fact]
    public async Task QuiescenceRequiresConsecutiveEmptySnapshots()
    {
        var lateChild = new ApplicationProcessSnapshot(
            700,
            70,
            ExecutablePath,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            HasMainWindow: false);
        var backend = new FakeProcessBackend();
        var snapshots = new Queue<IReadOnlyList<ApplicationProcessSnapshot>>(
            [
                [],
                [lateChild],
                [],
                [],
                []
            ]);
        backend.OnSnapshot = () =>
            snapshots.Count > 0 ? snapshots.Dequeue() : [];
        var service = new ApplicationRestartService(
            backend,
            pollInterval: TimeSpan.FromMilliseconds(1));

        await service.WaitForQuiescenceAsync(
            Candidate(),
            TimeSpan.FromSeconds(1),
            requiredStableSamples: 3);

        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task QuiescenceTimesOutWhileAnyMatchingProcessRemains()
    {
        var backend = new FakeProcessBackend(
            new ApplicationProcessSnapshot(
                800,
                80,
                ExecutablePath,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                HasMainWindow: false));
        var service = new ApplicationRestartService(
            backend,
            pollInterval: TimeSpan.FromMilliseconds(2));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            service.WaitForQuiescenceAsync(
                Candidate(),
                TimeSpan.FromMilliseconds(20)));
    }

    private static ApplicationCandidate Candidate() =>
        new(
            "ChatGPT / Codex",
            "ChatGPT",
            ExecutablePath,
            ExecutablePath,
            null,
            "test",
            "ChatGPT",
            "X64",
            ApplicationLaunchMethod.Executable,
            100,
            IsRunning: true);

    private sealed class FakeProcessBackend(
        params ApplicationProcessSnapshot[] snapshots) : IApplicationProcessBackend
    {
        public List<ApplicationProcessSnapshot> Snapshots { get; } = [.. snapshots];
        public List<ApplicationProcessIdentity> CloseRequests { get; } = [];
        public List<ApplicationProcessIdentity> KillRequests { get; } = [];
        public Func<ApplicationProcessIdentity, bool>? OnClose { get; set; }
        public Action<ApplicationProcessIdentity>? OnKill { get; set; }
        public Exception? SnapshotException { get; set; }
        public Func<IReadOnlyList<ApplicationProcessSnapshot>>? OnSnapshot { get; set; }

        public IReadOnlyList<ApplicationProcessSnapshot> Snapshot(string processName)
        {
            if (SnapshotException is not null)
            {
                throw SnapshotException;
            }
            return OnSnapshot?.Invoke() ?? Snapshots.ToArray();
        }

        public bool RequestClose(ApplicationProcessIdentity identity)
        {
            CloseRequests.Add(identity);
            return OnClose?.Invoke(identity) ?? false;
        }

        public void KillTree(ApplicationProcessIdentity identity)
        {
            KillRequests.Add(identity);
            OnKill?.Invoke(identity);
        }
    }
}
