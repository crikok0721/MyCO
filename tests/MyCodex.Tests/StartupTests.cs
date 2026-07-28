using MyCodex.Startup;

namespace MyCodex.Tests;

public sealed class StartupTests
{
    [Fact]
    public void StartupCommandQuotesSpacesAndChinesePaths()
    {
        var path = @"C:\程序 文件\MyCodex\MyCodex.exe";
        Assert.Equal(
            @"""C:\程序 文件\MyCodex\MyCodex.exe"" --background",
            StartupRegistrationService.BuildCommand(path));
    }

    [Fact]
    public void StartupRegistrationCreatesCorrectsAndDeletesOnlyItsOwnValue()
    {
        var backend = new FakeRunKeyBackend();
        backend.Values["OtherApp"] = "other.exe";
        var service = new StartupRegistrationService(backend, "MyCodex.Test");
        var firstPath = @"C:\Apps\MyCodex.exe";
        var movedPath = @"D:\New Folder\MyCodex.exe";

        service.SetEnabled(firstPath, enabled: true);
        Assert.True(service.GetStatus(firstPath).MatchesCurrentExecutable);
        Assert.False(service.GetStatus(movedPath).MatchesCurrentExecutable);

        service.SetEnabled(movedPath, enabled: true);
        Assert.True(service.GetStatus(movedPath).MatchesCurrentExecutable);

        var prior = new StartupRegistrationStatus(
            true,
            false,
            @"""E:\Old Folder\MyCodex.exe"" --background");
        service.Restore(prior);
        Assert.Equal(
            prior.RegisteredCommand,
            backend.Values["MyCodex.Test"]);

        service.SetEnabled(movedPath, enabled: false);
        Assert.False(service.GetStatus(movedPath).IsRegistered);
        Assert.Equal("other.exe", backend.Values["OtherApp"]);
    }

    [Fact]
    public void RealRegistryIntegrationUsesRandomValueAndAlwaysCleansUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var valueName = $"MyCodex.Tests.{Guid.NewGuid():N}";
        var service = new StartupRegistrationService(
            new StartupRegistrationService.RegistryRunKeyBackend(),
            valueName);
        var path = Path.Combine(
            Path.GetTempPath(),
            "MyCodex 测试",
            "MyCodex.exe");
        try
        {
            service.SetEnabled(path, enabled: true);
            var status = service.GetStatus(path);
            Assert.True(status.IsRegistered);
            Assert.True(status.MatchesCurrentExecutable);
        }
        finally
        {
            service.SetEnabled(path, enabled: false);
        }
        Assert.False(service.GetStatus(path).IsRegistered);
    }

    [Theory]
    [InlineData(false, true, false, false, AutomaticCodexLaunchDecision.Disabled)]
    [InlineData(true, false, false, false, AutomaticCodexLaunchDecision.DesktopNotFound)]
    [InlineData(true, true, false, true, AutomaticCodexLaunchDecision.AlreadyControlled)]
    [InlineData(true, true, true, false, AutomaticCodexLaunchDecision.AlreadyRunningUncontrolled)]
    [InlineData(true, true, false, false, AutomaticCodexLaunchDecision.Start)]
    public void AutomaticLaunchPolicyNeverDuplicatesRunningCodex(
        bool enabled,
        bool candidateFound,
        bool running,
        bool connected,
        AutomaticCodexLaunchDecision expected)
    {
        Assert.Equal(
            expected,
            AutomaticCodexLaunchPolicy.Decide(
                enabled,
                candidateFound,
                running,
                connected));
    }

    private sealed class FakeRunKeyBackend :
        StartupRegistrationService.IRunKeyBackend
    {
        public Dictionary<string, string> Values { get; } =
            new(StringComparer.Ordinal);

        public string? Read(string valueName) =>
            Values.GetValueOrDefault(valueName);

        public void Write(string valueName, string command) =>
            Values[valueName] = command;

        public void Delete(string valueName) =>
            Values.Remove(valueName);
    }
}
