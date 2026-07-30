using MyCO.VisualAcceptance;

namespace MyCO.Tests;

public sealed class VisualAcceptanceSafetyTests
{
    [Fact]
    public void RunPaths_UseAnIsolatedTempProfile()
    {
        var paths = VisualAcceptanceRunPaths.Create(
            "0123456789abcdef0123456789abcdef");

        Assert.StartsWith(
            Path.Combine(Path.GetTempPath(), "MyCO", "VisualAcceptance"),
            paths.RunDirectory,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            Path.Combine(paths.RunDirectory, "profile"),
            paths.ProfileDirectory);
        Assert.DoesNotContain(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            paths.ProfileDirectory,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("short")]
    public void RunPaths_RejectInvalidOrEscapingRunIds(string runId)
    {
        Assert.Throws<ArgumentException>(
            () => VisualAcceptanceRunPaths.Create(runId));
    }

    [Fact]
    public void RecursiveCleanup_RejectsAnythingOutsideTheOwnedRun()
    {
        var paths = VisualAcceptanceRunPaths.Create(
            "1123456789abcdef0123456789abcdef");

        Assert.Throws<InvalidOperationException>(
            () => paths.ValidateForRecursiveCleanup(paths.RootDirectory));
    }

    [Fact]
    public void ExactOwnedTarget_AcceptsOnlyTheRecordedIdentity()
    {
        var paths = VisualAcceptanceRunPaths.Create(
            "2123456789abcdef0123456789abcdef");
        var runStartedAt = DateTimeOffset.UtcNow.AddSeconds(-2);
        var processStartedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        var expected = Identity(
            paths,
            processId: 41001,
            executable: @"C:\Program Files\WindowsApps\OpenAI.Codex\app\ChatGPT.exe",
            processStartedAt,
            runStartedAt);

        Assert.True(VisualAcceptanceProcessGuard.IsExactOwnedTarget(
            expected,
            expected,
            paths));
    }

    [Fact]
    public void ExactOwnedTarget_NeverAcceptsAnotherSameExecutableProcess()
    {
        var paths = VisualAcceptanceRunPaths.Create(
            "3123456789abcdef0123456789abcdef");
        var runStartedAt = DateTimeOffset.UtcNow.AddSeconds(-2);
        var processStartedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        var expected = Identity(
            paths,
            processId: 42001,
            executable: @"C:\Program Files\WindowsApps\OpenAI.Codex\app\ChatGPT.exe",
            processStartedAt,
            runStartedAt);
        var otherCodex = expected with { ProcessId = 42002 };

        Assert.False(VisualAcceptanceProcessGuard.IsExactOwnedTarget(
            expected,
            otherCodex,
            paths));
    }

    [Fact]
    public void ExactOwnedTarget_FailsClosedForOldProcessOrDifferentProfile()
    {
        var paths = VisualAcceptanceRunPaths.Create(
            "4123456789abcdef0123456789abcdef");
        var runStartedAt = DateTimeOffset.UtcNow;
        var expected = Identity(
            paths,
            processId: 43001,
            executable: @"C:\OpenAI\ChatGPT.exe",
            processStartedAt: runStartedAt.AddSeconds(1),
            runStartedAt);

        Assert.False(VisualAcceptanceProcessGuard.IsExactOwnedTarget(
            expected,
            expected with { ProcessStartedAt = runStartedAt.AddSeconds(-1) },
            paths));
        Assert.False(VisualAcceptanceProcessGuard.IsExactOwnedTarget(
            expected,
            expected with { ProfilePath = Path.Combine(paths.RunDirectory, "other") },
            paths));
    }

    [Fact]
    public void LaunchArguments_AlwaysUsePrivatePipeAndTheIsolatedProfile()
    {
        var paths = VisualAcceptanceRunPaths.Create(
            "5123456789abcdef0123456789abcdef");

        var arguments = VisualAcceptanceLaunchArguments.Create(
            paths.ProfileDirectory);

        Assert.Contains("--remote-debugging-pipe", arguments);
        Assert.Contains(
            $"--user-data-dir={Path.GetFullPath(paths.ProfileDirectory)}",
            arguments);
        Assert.DoesNotContain(
            arguments,
            argument => argument.StartsWith(
                "--remote-debugging-port",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Lifecycle_AllowsStartRestartDisableAndStopButRejectsSkipping()
    {
        var phases = new[]
        {
            "host-starting",
            "launching",
            "ready",
            "restarting",
            "ready",
            "disabled",
            "stopping",
            "stopped",
            "cleaned"
        };
        for (var index = 0; index < phases.Length - 1; index++)
        {
            VisualAcceptanceLifecycle.EnsureTransition(
                phases[index],
                phases[index + 1]);
        }

        Assert.Throws<InvalidOperationException>(
            () => VisualAcceptanceLifecycle.EnsureTransition(
                "host-starting",
                "stopped"));
    }

    private static VisualAcceptanceProcessIdentity Identity(
        VisualAcceptanceRunPaths paths,
        int processId,
        string executable,
        DateTimeOffset processStartedAt,
        DateTimeOffset runStartedAt) =>
        new(
            paths.RunId,
            processId,
            executable,
            processStartedAt,
            runStartedAt,
            paths.ProfileDirectory);
}
