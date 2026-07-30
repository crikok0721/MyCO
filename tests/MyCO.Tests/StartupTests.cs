using MyCO.Startup;

namespace MyCO.Tests;

public sealed class StartupTests
{
    [Fact]
    public void StartupCommandQuotesSpacesAndChinesePaths()
    {
        var path = @"C:\程序 文件\MyCO\MyCO.exe";
        Assert.Equal(
            @"""C:\程序 文件\MyCO\MyCO.exe"" --background",
            StartupRegistrationService.BuildCommand(path));
    }

    [Fact]
    public void StartupRegistrationCreatesCorrectsAndDeletesOnlyItsOwnValue()
    {
        var backend = new FakeRunKeyBackend();
        backend.Values["OtherApp"] = "other.exe";
        var service = new StartupRegistrationService(backend, "MyCO.Test");
        var firstPath = @"C:\Apps\MyCO.exe";
        var movedPath = @"D:\New Folder\MyCO.exe";

        service.SetEnabled(firstPath, enabled: true);
        Assert.True(service.GetStatus(firstPath).MatchesCurrentExecutable);
        Assert.False(service.GetStatus(movedPath).MatchesCurrentExecutable);

        service.SetEnabled(movedPath, enabled: true);
        Assert.True(service.GetStatus(movedPath).MatchesCurrentExecutable);

        var prior = new StartupRegistrationStatus(
            true,
            false,
            @"""E:\Old Folder\MyCO.exe"" --background");
        service.Restore(prior);
        Assert.Equal(
            prior.RegisteredCommand,
            backend.Values["MyCO.Test"]);

        service.SetEnabled(movedPath, enabled: false);
        Assert.False(service.GetStatus(movedPath).IsRegistered);
        Assert.Equal("other.exe", backend.Values["OtherApp"]);
    }

    [Fact]
    public void StartupRegistrationMigratesPriorBrandValuesWithoutDuplicates()
    {
        var backend = new FakeRunKeyBackend();
        var legacyCommand = @"""C:\Apps\MyCodex\MyCodex.exe"" --background";
        var transitionalCommand = @"""C:\Apps\Myco\MyCO.exe"" --background";
        backend.Values["MyCodex"] = legacyCommand;
        backend.Values["Myco"] = transitionalCommand;
        var service = new StartupRegistrationService(
            backend,
            "MyCO",
            "Myco",
            "MyCodex");
        var newPath = @"C:\Apps\MyCO\MyCO.exe";

        var legacyStatus = service.GetStatus(newPath);
        Assert.True(legacyStatus.IsRegistered);
        Assert.False(legacyStatus.MatchesCurrentExecutable);
        Assert.Equal(transitionalCommand, legacyStatus.RegisteredCommand);

        service.SetEnabled(newPath, enabled: true);

        Assert.Equal(
            @"""C:\Apps\MyCO\MyCO.exe"" --background",
            backend.Values["MyCO"]);
        Assert.False(backend.Values.ContainsKey("Myco"));
        Assert.False(backend.Values.ContainsKey("MyCodex"));

        service.SetEnabled(newPath, enabled: false);
        Assert.False(backend.Values.ContainsKey("MyCO"));
        Assert.False(backend.Values.ContainsKey("Myco"));
        Assert.False(backend.Values.ContainsKey("MyCodex"));
    }

    [Fact]
    public void RealRegistryIntegrationUsesRandomValueAndAlwaysCleansUp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var valueName = $"MyCO.Tests.{Guid.NewGuid():N}";
        var service = new StartupRegistrationService(
            new StartupRegistrationService.RegistryRunKeyBackend(),
            valueName);
        var path = Path.Combine(
            Path.GetTempPath(),
            "MyCO 测试",
            "MyCO.exe");
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

    [Fact]
    public void RealRegistryIntegrationNormalizesCaseOnlyBrandValue()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var suffix = Guid.NewGuid().ToString("N");
        var currentName = $"MyCO.Case.{suffix}";
        var transitionalName = $"Myco.Case.{suffix}";
        var backend = new StartupRegistrationService.RegistryRunKeyBackend();
        var service = new StartupRegistrationService(
            backend,
            currentName,
            transitionalName);
        var oldPath = Path.Combine(Path.GetTempPath(), "Myco", "MyCO.exe");
        var newPath = Path.Combine(Path.GetTempPath(), "MyCO", "MyCO.exe");

        try
        {
            backend.Write(
                transitionalName,
                StartupRegistrationService.BuildCommand(oldPath));

            Assert.True(service.GetStatus(newPath).IsRegistered);
            Assert.False(service.GetStatus(newPath).MatchesCurrentExecutable);

            service.SetEnabled(newPath, enabled: true);

            Assert.True(backend.ExistsExact(currentName));
            Assert.False(backend.ExistsExact(transitionalName));
            Assert.True(service.GetStatus(newPath).MatchesCurrentExecutable);
        }
        finally
        {
            service.SetEnabled(newPath, enabled: false);
        }
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

        public bool ExistsExact(string valueName) => Values.ContainsKey(valueName);

        public string? Read(string valueName) =>
            Values.GetValueOrDefault(valueName);

        public void Write(string valueName, string command) =>
            Values[valueName] = command;

        public void Delete(string valueName) =>
            Values.Remove(valueName);
    }
}
