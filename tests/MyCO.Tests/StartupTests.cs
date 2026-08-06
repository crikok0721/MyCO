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

    [Fact]
    public void CodexAssociationRepairsStaleOwnedEntriesAndIsIdempotent()
    {
        var backend = new FakeAssociationBackend();
        var service = new CodexLaunchAssociationService(backend);
        var executable = Path.Combine(
            Path.GetTempPath(),
            "MyCO Association Tests",
            "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        var staleExecutable = Path.Combine(Path.GetTempPath(), "Old MyCO", "MyCO.exe");
        backend.Shortcuts[paths.StartMenuShortcut] =
            new(staleExecutable, "--codex-launch", IsOwned: true);
        backend.Protocols[paths.ProtocolCommand] =
            new(staleExecutable, IsOwned: true);

        service.SetEnabled(executable, enabled: true);

        var status = service.GetStatus(executable);
        Assert.True(status.IsEnabled);
        Assert.Equal(2, backend.Shortcuts.Count);
        Assert.Single(backend.Protocols);
        Assert.All(backend.Shortcuts.Values, entry =>
            Assert.Equal(executable, entry.ExecutablePath));
        Assert.Equal(executable, backend.Protocols[paths.ProtocolCommand].ExecutablePath);
        var writes = backend.MutationCount;

        service.SetEnabled(executable, enabled: true);

        Assert.Equal(writes, backend.MutationCount);

        service.SetEnabled(executable, enabled: false);

        Assert.Empty(backend.Shortcuts);
        Assert.Empty(backend.Protocols);
        Assert.False(service.GetStatus(executable).IsEnabled);
    }

    [Fact]
    public void CodexAssociationRefusesForeignShortcutAndLeavesItUnchanged()
    {
        var backend = new FakeAssociationBackend();
        var service = new CodexLaunchAssociationService(backend);
        var executable = Path.Combine(Path.GetTempPath(), "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        backend.Shortcuts[paths.StartMenuShortcut] =
            new(@"C:\Foreign\Other.exe", "--codex-launch", IsOwned: false);
        backend.Protocols[paths.ProtocolCommand] =
            new(@"C:\Foreign\Other.exe", IsOwned: false);
        var before = backend.CloneState();

        Assert.Throws<IOException>(() => service.SetEnabled(executable, enabled: true));
        Assert.Equal(before, backend.CloneState());
        Assert.Equal(0, backend.MutationCount);
    }

    [Fact]
    public void CodexAssociationRollsBackEveryEntryWhenEnableOrDisableFails()
    {
        var executable = Path.Combine(Path.GetTempPath(), "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        var backend = new FakeAssociationBackend { FailOnMutation = 2 };
        var service = new CodexLaunchAssociationService(backend);
        var missing = backend.CloneState();

        Assert.Throws<IOException>(() => service.SetEnabled(executable, enabled: true));
        Assert.Equal(missing, backend.CloneState());

        backend.FailOnMutation = null;
        service.SetEnabled(executable, enabled: true);
        var enabled = backend.CloneState();
        backend.MutationCount = 0;
        backend.FailOnMutation = 2;

        Assert.Throws<IOException>(() => service.SetEnabled(executable, enabled: false));
        Assert.Equal(enabled, backend.CloneState());
        Assert.True(backend.Shortcuts.ContainsKey(paths.StartMenuShortcut));
        Assert.True(backend.Shortcuts.ContainsKey(paths.DesktopShortcut));
        Assert.True(backend.Protocols.ContainsKey(paths.ProtocolCommand));
    }

    [Fact]
    public void CodexAssociationRollbackPreservesEntryChangedByAnotherWriter()
    {
        var executable = Path.Combine(Path.GetTempPath(), "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        var backend = new FakeAssociationBackend { FailOnMutation = 2 };
        var service = new CodexLaunchAssociationService(backend);
        backend.OnInjectedFailure = () => backend.Shortcuts[paths.StartMenuShortcut] =
            new(@"C:\Foreign\Other.exe", "--codex-launch", IsOwned: false);

        Assert.Throws<IOException>(() => service.SetEnabled(executable, enabled: true));

        Assert.Equal(
            @"C:\Foreign\Other.exe",
            backend.Shortcuts[paths.StartMenuShortcut].ExecutablePath);
        Assert.False(backend.Shortcuts[paths.StartMenuShortcut].IsOwned);
    }

    [Fact]
    public void AssociationSnapshotRestoresPartialAndPathDriftStateExactly()
    {
        var executable = Path.Combine(Path.GetTempPath(), "Current", "MyCO.exe");
        var stale = Path.Combine(Path.GetTempPath(), "Old", "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        var backend = new FakeAssociationBackend();
        var service = new CodexLaunchAssociationService(backend);
        backend.Shortcuts[paths.StartMenuShortcut] =
            new(stale, "--codex-launch", IsOwned: true);
        backend.Shortcuts[paths.DesktopShortcut] =
            new(executable, "--codex-launch", IsOwned: true);
        var before = backend.CloneState();

        var snapshot = service.CaptureSnapshot(executable);
        service.SetEnabled(executable, enabled: false, snapshot);
        Assert.Empty(backend.Shortcuts);

        service.Restore(snapshot);

        Assert.Equal(before, backend.CloneState());
    }

    [Fact]
    public void AssociationSnapshotRestoreDoesNotOverwriteForeignGeneration()
    {
        var executable = Path.Combine(Path.GetTempPath(), "Current", "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        var backend = new FakeAssociationBackend();
        var service = new CodexLaunchAssociationService(backend);

        var snapshot = service.CaptureSnapshot(executable);
        service.SetEnabled(executable, enabled: true, snapshot);
        backend.Shortcuts[paths.DesktopShortcut] =
            new(@"C:\Foreign\Other.exe", "--codex-launch", IsOwned: false);

        service.Restore(snapshot);

        Assert.False(backend.Shortcuts.ContainsKey(paths.StartMenuShortcut));
        Assert.Equal(
            @"C:\Foreign\Other.exe",
            backend.Shortcuts[paths.DesktopShortcut].ExecutablePath);
        Assert.False(backend.Protocols.ContainsKey(paths.ProtocolCommand));
    }

    [Fact]
    public void CodexAssociationDeletesOnlyExactOwnedProtocolAndDisableIsIdempotent()
    {
        var executable = Path.Combine(Path.GetTempPath(), "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(executable);
        var backend = new FakeAssociationBackend();
        var service = new CodexLaunchAssociationService(backend);
        backend.Protocols[paths.ProtocolCommand] =
            new(executable, IsOwned: false);

        service.SetEnabled(executable, enabled: false);
        Assert.True(backend.Protocols.ContainsKey(paths.ProtocolCommand));

        backend.Protocols[paths.ProtocolCommand] =
            new(executable, IsOwned: true);
        service.SetEnabled(executable, enabled: false);
        Assert.False(backend.Protocols.ContainsKey(paths.ProtocolCommand));
        var mutations = backend.MutationCount;

        service.SetEnabled(executable, enabled: false);
        Assert.Equal(mutations, backend.MutationCount);
    }

    [Fact]
    public void AppIdentityShortcutRepairsPathDriftButRefusesForeignOwner()
    {
        var current = Path.Combine(Path.GetTempPath(), "Current", "MyCO.exe");
        var stale = Path.Combine(Path.GetTempPath(), "Old", "MyCO.exe");
        var paths = CodexLaunchAssociationService.AssociationPaths.For(current);
        var backend = new FakeAssociationBackend();
        var service = new CodexLaunchAssociationService(backend);
        backend.Shortcuts[paths.AppIdentityShortcut] =
            new(stale, string.Empty, IsOwned: true);

        Assert.True(service.TryEnsureAppIdentityShortcut(current));
        Assert.Equal(
            current,
            backend.Shortcuts[paths.AppIdentityShortcut].ExecutablePath);
        Assert.Equal(string.Empty, backend.Shortcuts[paths.AppIdentityShortcut].Arguments);

        backend.Shortcuts[paths.AppIdentityShortcut] =
            new(@"C:\Foreign\Other.exe", string.Empty, IsOwned: false);
        var before = backend.CloneState();

        Assert.False(service.TryEnsureAppIdentityShortcut(current));
        Assert.Equal(before, backend.CloneState());
    }

    [Fact]
    public void WindowsAssociationBackendWritesAndReloadsShellLinkMetadata()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var executable = Path.Combine(directory.Path, "MyCO.exe");
        var shortcut = Path.Combine(directory.Path, "MyCO.lnk");
        File.WriteAllBytes(executable, []);

        var backend = new CodexLaunchAssociationService.WindowsAssociationBackend(
            [directory.Path]);

        backend.WriteShortcut(
            shortcut,
            executable,
            "--codex-launch",
            CodexLaunchAssociationService.AppUserModelId,
            replaceExisting: false);

        var metadata = backend.ReadShortcutMetadata(shortcut);
        Assert.NotNull(metadata);
        Assert.Equal(
            Path.GetFullPath(executable),
            Path.GetFullPath(metadata.ExecutablePath),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal("--codex-launch", metadata.Arguments);
        Assert.Equal(CodexLaunchAssociationService.AppUserModelId, metadata.AppUserModelId);
        Assert.Equal(
            Path.GetFullPath(executable),
            Path.GetFullPath(metadata.IconPath),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            CodexLaunchAssociationService.AssociationEntryState.CurrentOwned,
            backend.InspectShortcut(shortcut, executable, "--codex-launch"));
    }

    [Fact]
    public void WindowsAssociationBackendLeavesMalformedAndReparseShortcutsForeign()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var executable = Path.Combine(directory.Path, "MyCO.exe");
        var malformed = Path.Combine(directory.Path, "Malformed.lnk");
        var target = Path.Combine(directory.Path, "Target.lnk");
        var reparse = Path.Combine(directory.Path, "Reparse.lnk");
        File.WriteAllBytes(executable, []);
        File.WriteAllText(malformed, "not a shell link");
        var backend = new CodexLaunchAssociationService.WindowsAssociationBackend(
            [directory.Path]);
        backend.WriteShortcut(
            target,
            executable,
            "--codex-launch",
            CodexLaunchAssociationService.AppUserModelId,
            replaceExisting: false);

        Assert.Equal(
            CodexLaunchAssociationService.AssociationEntryState.Foreign,
            backend.InspectShortcut(malformed, executable, "--codex-launch"));

        try
        {
            File.CreateSymbolicLink(reparse, target);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            return;
        }
        Assert.Equal(
            CodexLaunchAssociationService.AssociationEntryState.Foreign,
            backend.InspectShortcut(reparse, executable, "--codex-launch"));
    }

    [Fact]
    public void WindowsAssociationBackendRejectsReparsePointInTrustedPathAncestor()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var trustedRoot = Path.Combine(directory.Path, "Trusted");
        var targetDirectory = Path.Combine(directory.Path, "Target");
        var linkedDirectory = Path.Combine(trustedRoot, "Linked");
        Directory.CreateDirectory(trustedRoot);
        Directory.CreateDirectory(targetDirectory);
        try
        {
            Directory.CreateSymbolicLink(linkedDirectory, targetDirectory);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            return;
        }

        var executable = Path.Combine(directory.Path, "MyCO.exe");
        File.WriteAllBytes(executable, []);
        var shortcut = Path.Combine(linkedDirectory, "MyCO.lnk");
        var backend = new CodexLaunchAssociationService.WindowsAssociationBackend(
            [trustedRoot, targetDirectory]);

        var targetShortcut = Path.Combine(targetDirectory, "MyCO.lnk");
        backend.WriteShortcut(
            targetShortcut,
            executable,
            "--codex-launch",
            CodexLaunchAssociationService.AppUserModelId,
            replaceExisting: false);

        Assert.Throws<IOException>(() => backend.WriteShortcut(
            shortcut,
            executable,
            "--codex-launch",
            CodexLaunchAssociationService.AppUserModelId,
            replaceExisting: false));
        Assert.Throws<IOException>(() =>
            backend.CaptureShortcutRollback(shortcut));
        Assert.Throws<IOException>(() => backend.DeleteShortcut(
            shortcut,
            executable,
            "--codex-launch"));
        Assert.True(File.Exists(targetShortcut));
    }

    [Fact]
    public void ShortcutRollbackRejectsAncestorChangedToReparsePoint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TempDirectory();
        var trustedRoot = Path.Combine(directory.Path, "Trusted");
        var ownedDirectory = Path.Combine(trustedRoot, "Owned");
        var targetDirectory = Path.Combine(directory.Path, "Target");
        Directory.CreateDirectory(ownedDirectory);
        Directory.CreateDirectory(targetDirectory);
        var executable = Path.Combine(directory.Path, "MyCO.exe");
        File.WriteAllBytes(executable, []);
        var shortcut = Path.Combine(ownedDirectory, "MyCO.lnk");
        var backend = new CodexLaunchAssociationService.WindowsAssociationBackend(
            [trustedRoot]);
        backend.WriteShortcut(
            shortcut,
            executable,
            "--codex-launch",
            CodexLaunchAssociationService.AppUserModelId,
            replaceExisting: false);
        var rollback = backend.CaptureShortcutRollback(shortcut);
        backend.DeleteShortcut(shortcut, executable, "--codex-launch");
        rollback.SealExpectedGeneration();
        Directory.Delete(ownedDirectory);
        try
        {
            Directory.CreateSymbolicLink(ownedDirectory, targetDirectory);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            return;
        }

        Assert.Throws<IOException>(() => rollback.RestoreIfExpected());
        Assert.False(File.Exists(Path.Combine(targetDirectory, "MyCO.lnk")));
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

    private sealed class FakeAssociationBackend :
        CodexLaunchAssociationService.IAssociationBackend
    {
        public Dictionary<string, ShortcutEntry> Shortcuts { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ProtocolEntry> Protocols { get; } =
            new(StringComparer.Ordinal);
        public int MutationCount { get; set; }
        public int? FailOnMutation { get; set; }
        public Action? OnInjectedFailure { get; set; }

        public CodexLaunchAssociationService.AssociationEntryState InspectShortcut(
            string path,
            string executablePath,
            string arguments)
        {
            if (!Shortcuts.TryGetValue(path, out var entry))
            {
                return CodexLaunchAssociationService.AssociationEntryState.Missing;
            }
            if (!entry.IsOwned || !string.Equals(entry.Arguments, arguments, StringComparison.Ordinal))
            {
                return CodexLaunchAssociationService.AssociationEntryState.Foreign;
            }
            return string.Equals(
                entry.ExecutablePath,
                executablePath,
                StringComparison.OrdinalIgnoreCase)
                ? CodexLaunchAssociationService.AssociationEntryState.CurrentOwned
                : CodexLaunchAssociationService.AssociationEntryState.StaleOwned;
        }

        public void WriteShortcut(
            string path,
            string executablePath,
            string arguments,
            string appUserModelId,
            bool replaceExisting)
        {
            ThrowIfRequested();
            Shortcuts[path] = new(executablePath, arguments, IsOwned: true);
        }

        public void DeleteShortcut(
            string path,
            string executablePath,
            string arguments)
        {
            ThrowIfRequested();
            Shortcuts.Remove(path);
        }

        public CodexLaunchAssociationService.AssociationEntryState InspectProtocol(
            string commandKey,
            string executablePath)
        {
            if (!Protocols.TryGetValue(commandKey, out var entry))
            {
                return CodexLaunchAssociationService.AssociationEntryState.Missing;
            }
            if (!entry.IsOwned)
            {
                return CodexLaunchAssociationService.AssociationEntryState.Foreign;
            }
            return string.Equals(
                entry.ExecutablePath,
                executablePath,
                StringComparison.OrdinalIgnoreCase)
                ? CodexLaunchAssociationService.AssociationEntryState.CurrentOwned
                : CodexLaunchAssociationService.AssociationEntryState.StaleOwned;
        }

        public void WriteProtocol(string commandKey, string executablePath)
        {
            ThrowIfRequested();
            Protocols[commandKey] = new(executablePath, IsOwned: true);
        }

        public void DeleteProtocol(string commandKey, string executablePath)
        {
            ThrowIfRequested();
            Protocols.Remove(commandKey);
        }

        public CodexLaunchAssociationService.IAssociationEntrySnapshot
            CaptureShortcutRollback(string path)
        {
            var existed = Shortcuts.TryGetValue(path, out var entry);
            return new FakeEntrySnapshot<ShortcutEntry>(
                () => Shortcuts.GetValueOrDefault(path),
                value => SetOrRemove(Shortcuts, path, value),
                existed ? entry : null);
        }

        public CodexLaunchAssociationService.IAssociationEntrySnapshot
            CaptureProtocolRollback(string commandKey)
        {
            var existed = Protocols.TryGetValue(commandKey, out var entry);
            return new FakeEntrySnapshot<ProtocolEntry>(
                () => Protocols.GetValueOrDefault(commandKey),
                value => SetOrRemove(Protocols, commandKey, value),
                existed ? entry : null);
        }

        public string CloneState() => string.Join(
            "|",
            Shortcuts.OrderBy(pair => pair.Key).Select(pair =>
                $"S:{pair.Key}:{pair.Value}")
                .Concat(Protocols.OrderBy(pair => pair.Key).Select(pair =>
                    $"P:{pair.Key}:{pair.Value}")));

        private void ThrowIfRequested()
        {
            MutationCount++;
            if (MutationCount == FailOnMutation)
            {
                OnInjectedFailure?.Invoke();
                throw new IOException("injected transaction failure");
            }
        }

        public sealed record ShortcutEntry(
            string ExecutablePath,
            string Arguments,
            bool IsOwned);

        public sealed record ProtocolEntry(string ExecutablePath, bool IsOwned);

        private static void SetOrRemove<T>(
            IDictionary<string, T> entries,
            string key,
            T? value) where T : class
        {
            if (value is null)
            {
                entries.Remove(key);
            }
            else
            {
                entries[key] = value;
            }
        }

        private sealed class FakeEntrySnapshot<T> :
            CodexLaunchAssociationService.IAssociationEntrySnapshot
            where T : class
        {
            private readonly Func<T?> _read;
            private readonly Action<T?> _write;
            private readonly T? _original;
            private T? _expected;

            public FakeEntrySnapshot(
                Func<T?> read,
                Action<T?> write,
                T? original)
            {
                _read = read;
                _write = write;
                _original = original;
                _expected = original;
            }

            public void SealExpectedGeneration() => _expected = _read();

            public void RestoreIfExpected()
            {
                if (Equals(_read(), _expected))
                {
                    _write(_original);
                }
            }
        }
    }
}
