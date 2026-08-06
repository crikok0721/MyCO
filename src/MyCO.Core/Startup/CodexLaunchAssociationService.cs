using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace MyCO.Startup;

public sealed record CodexLaunchAssociationStatus(
    bool IsEnabled,
    string StartMenuShortcut,
    string DesktopShortcut,
    string ProtocolCommand);

public sealed class CodexLaunchAssociationSnapshot
{
    internal CodexLaunchAssociationSnapshot(
        object owner,
        string executablePath,
        string startMenuShortcut,
        string desktopShortcut,
        string protocolCommand,
        CodexLaunchAssociationService.IAssociationEntrySnapshot startMenu,
        CodexLaunchAssociationService.IAssociationEntrySnapshot desktop,
        CodexLaunchAssociationService.IAssociationEntrySnapshot protocol)
    {
        Owner = owner;
        ExecutablePath = executablePath;
        StartMenuShortcut = startMenuShortcut;
        DesktopShortcut = desktopShortcut;
        ProtocolCommand = protocolCommand;
        Entries = [startMenu, desktop, protocol];
    }

    internal object Owner { get; }
    internal string ExecutablePath { get; }
    internal string StartMenuShortcut { get; }
    internal string DesktopShortcut { get; }
    internal string ProtocolCommand { get; }
    internal IReadOnlyList<CodexLaunchAssociationService.IAssociationEntrySnapshot>
        Entries { get; }
}

// Registers only MyCO-owned launch surfaces. It never rewrites an existing
// official Codex shortcut or protocol registration that belongs to another app.
public sealed class CodexLaunchAssociationService
{
    public const string ProtocolScheme = "myco-codex";
    public const string AppUserModelId = "Crikok.MyCO";
    private const string CodexArguments = "--codex-launch";

    private readonly IAssociationBackend _backend;

    public CodexLaunchAssociationService()
        : this(new WindowsAssociationBackend())
    {
    }

    internal CodexLaunchAssociationService(IAssociationBackend backend)
    {
        _backend = backend;
    }

    public CodexLaunchAssociationStatus GetStatus(string executablePath)
    {
        var paths = AssociationPaths.For(executablePath);
        return new CodexLaunchAssociationStatus(
            _backend.InspectShortcut(
                paths.StartMenuShortcut,
                paths.ExecutablePath,
                CodexArguments) == AssociationEntryState.CurrentOwned &&
            _backend.InspectShortcut(
                paths.DesktopShortcut,
                paths.ExecutablePath,
                CodexArguments) == AssociationEntryState.CurrentOwned &&
            _backend.InspectProtocol(
                paths.ProtocolCommand,
                paths.ExecutablePath) == AssociationEntryState.CurrentOwned,
            paths.StartMenuShortcut,
            paths.DesktopShortcut,
            paths.ProtocolCommand);
    }

    public void SetEnabled(string executablePath, bool enabled)
    {
        var snapshot = CaptureSnapshot(executablePath);
        SetEnabled(executablePath, enabled, snapshot);
    }

    public CodexLaunchAssociationSnapshot CaptureSnapshot(string executablePath)
    {
        var paths = AssociationPaths.For(executablePath);
        return new CodexLaunchAssociationSnapshot(
            _backend,
            paths.ExecutablePath,
            paths.StartMenuShortcut,
            paths.DesktopShortcut,
            paths.ProtocolCommand,
            _backend.CaptureShortcutRollback(paths.StartMenuShortcut),
            _backend.CaptureShortcutRollback(paths.DesktopShortcut),
            _backend.CaptureProtocolRollback(paths.ProtocolCommand));
    }

    public void SetEnabled(
        string executablePath,
        bool enabled,
        CodexLaunchAssociationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var paths = AssociationPaths.For(executablePath);
        ValidateSnapshot(snapshot, paths);
        var startMenuState = _backend.InspectShortcut(
            paths.StartMenuShortcut,
            paths.ExecutablePath,
            CodexArguments);
        var desktopState = _backend.InspectShortcut(
            paths.DesktopShortcut,
            paths.ExecutablePath,
            CodexArguments);
        var protocolState = _backend.InspectProtocol(
            paths.ProtocolCommand,
            paths.ExecutablePath);

        if (enabled &&
            (startMenuState == AssociationEntryState.Foreign ||
             desktopState == AssociationEntryState.Foreign ||
             protocolState == AssociationEntryState.Foreign))
        {
            throw new IOException(
                "A MyCO launch entry is already owned by another application.");
        }

        ExecuteTransaction(snapshot.Entries, () =>
        {
            if (enabled)
            {
                EnsureCodexShortcut(
                    paths.StartMenuShortcut,
                    paths.ExecutablePath,
                    startMenuState,
                    snapshot.Entries[0]);
                EnsureCodexShortcut(
                    paths.DesktopShortcut,
                    paths.ExecutablePath,
                    desktopState,
                    snapshot.Entries[1]);
                if (protocolState is AssociationEntryState.Missing or
                    AssociationEntryState.StaleOwned)
                {
                    if (_backend.InspectProtocol(
                            paths.ProtocolCommand,
                            paths.ExecutablePath) != protocolState)
                    {
                        throw new AssociationEntryChangedException();
                    }
                    Mutate(snapshot.Entries[2], () => _backend.WriteProtocol(
                        paths.ProtocolCommand,
                        paths.ExecutablePath));
                }
                return;
            }

            if (startMenuState is AssociationEntryState.CurrentOwned or
                AssociationEntryState.StaleOwned)
            {
                DeleteCodexShortcut(
                    paths.StartMenuShortcut,
                    paths.ExecutablePath,
                    startMenuState,
                    snapshot.Entries[0]);
            }
            if (desktopState is AssociationEntryState.CurrentOwned or
                AssociationEntryState.StaleOwned)
            {
                DeleteCodexShortcut(
                    paths.DesktopShortcut,
                    paths.ExecutablePath,
                    desktopState,
                    snapshot.Entries[1]);
            }
            if (protocolState is AssociationEntryState.CurrentOwned or
                AssociationEntryState.StaleOwned)
            {
                Mutate(snapshot.Entries[2], () => _backend.DeleteProtocol(
                    paths.ProtocolCommand,
                    paths.ExecutablePath));
            }
        });
    }

    public void Restore(CodexLaunchAssociationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot.Owner, _backend))
        {
            throw new InvalidOperationException(
                "The association snapshot belongs to another backend instance.");
        }
        RestoreEntries(snapshot.Entries);
    }

    // The Start-menu application identity gives legacy NotifyIcon balloons a
    // stable MyCO owner. Startup treats this as best effort and fails closed if
    // another application occupies the path.
    public bool TryEnsureAppIdentityShortcut(string executablePath)
    {
        try
        {
            var paths = AssociationPaths.For(executablePath);
            var state = _backend.InspectShortcut(
                paths.AppIdentityShortcut,
                paths.ExecutablePath,
                string.Empty);
            if (state == AssociationEntryState.Foreign)
            {
                return false;
            }
            if (state == AssociationEntryState.CurrentOwned)
            {
                return true;
            }

            var rollback = _backend.CaptureShortcutRollback(
                paths.AppIdentityShortcut);
            ExecuteTransaction([rollback], () =>
                Mutate(rollback, () => _backend.WriteShortcut(
                    paths.AppIdentityShortcut,
                    paths.ExecutablePath,
                    string.Empty,
                    AppUserModelId,
                    replaceExisting: state == AssociationEntryState.StaleOwned)));
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException or COMException or
            AggregateException)
        {
            return false;
        }
    }

    private void EnsureCodexShortcut(
        string path,
        string executablePath,
        AssociationEntryState state,
        IAssociationEntrySnapshot rollback)
    {
        if (state is not (AssociationEntryState.Missing or
            AssociationEntryState.StaleOwned))
        {
            return;
        }
        if (_backend.InspectShortcut(path, executablePath, CodexArguments) != state)
        {
            throw new AssociationEntryChangedException();
        }
        Mutate(rollback, () => _backend.WriteShortcut(
            path,
            executablePath,
            CodexArguments,
            AppUserModelId,
            replaceExisting: state == AssociationEntryState.StaleOwned));
    }

    private void DeleteCodexShortcut(
        string path,
        string executablePath,
        AssociationEntryState expectedState,
        IAssociationEntrySnapshot rollback)
    {
        var current = _backend.InspectShortcut(
            path,
            executablePath,
            CodexArguments);
        if (current != expectedState ||
            current is not (AssociationEntryState.CurrentOwned or
                AssociationEntryState.StaleOwned))
        {
            throw new AssociationEntryChangedException();
        }
        Mutate(rollback, () => _backend.DeleteShortcut(
            path,
            executablePath,
            CodexArguments));
    }

    private static void Mutate(IAssociationEntrySnapshot snapshot, Action action)
    {
        try
        {
            action();
        }
        catch (AssociationEntryChangedException)
        {
            throw;
        }
        catch
        {
            snapshot.SealExpectedGeneration();
            throw;
        }
        snapshot.SealExpectedGeneration();
    }

    private static void ExecuteTransaction(
        IReadOnlyList<IAssociationEntrySnapshot> rollbacks,
        Action action)
    {
        try
        {
            action();
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            for (var index = rollbacks.Count - 1; index >= 0; index--)
            {
                try
                {
                    rollbacks[index].RestoreIfExpected();
                }
                catch (Exception rollbackError)
                {
                    errors.Add(rollbackError);
                }
            }
            if (errors.Count > 1)
            {
                throw new AggregateException(
                    "The launch association transaction and rollback failed.",
                    errors);
            }
            throw;
        }
    }

    private void ValidateSnapshot(
        CodexLaunchAssociationSnapshot snapshot,
        AssociationPathSet paths)
    {
        if (!ReferenceEquals(snapshot.Owner, _backend) ||
            !string.Equals(
                snapshot.ExecutablePath,
                paths.ExecutablePath,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.StartMenuShortcut,
                paths.StartMenuShortcut,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.DesktopShortcut,
                paths.DesktopShortcut,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.ProtocolCommand,
                paths.ProtocolCommand,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The association snapshot does not match this transaction.");
        }
    }

    private static void RestoreEntries(
        IReadOnlyList<IAssociationEntrySnapshot> entries)
    {
        List<Exception>? errors = null;
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            try
            {
                entries[index].RestoreIfExpected();
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }
        if (errors is not null)
        {
            throw new AggregateException(
                "One or more association entries could not be restored.",
                errors);
        }
    }

    internal sealed class AssociationEntryChangedException : IOException
    {
        public AssociationEntryChangedException()
            : base("The association entry changed during the transaction.")
        {
        }
    }

    internal enum AssociationEntryState
    {
        Missing,
        CurrentOwned,
        StaleOwned,
        Foreign
    }

    internal static class AssociationPaths
    {
        public static AssociationPathSet For(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException(
                    "Executable path is required.",
                    nameof(executablePath));
            }

            var executable = Path.GetFullPath(executablePath);
            if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Association target must be an executable.",
                    nameof(executablePath));
            }

            var programs = Path.Combine(
                RequiredFolder(Environment.SpecialFolder.StartMenu),
                "Programs",
                "MyCO");
            var startMenu = Path.Combine(programs, "MyCO - Codex.lnk");
            var identity = Path.Combine(programs, "MyCO.lnk");
            var desktop = Path.Combine(
                RequiredFolder(Environment.SpecialFolder.DesktopDirectory),
                "MyCO - Codex.lnk");
            var protocol =
                $"Software\\Classes\\{ProtocolScheme}\\shell\\open\\command";
            return new AssociationPathSet(
                executable,
                startMenu,
                desktop,
                identity,
                protocol);
        }

        private static string RequiredFolder(Environment.SpecialFolder folder)
        {
            var path = Environment.GetFolderPath(folder);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    $"The Windows folder {folder} is unavailable.");
            }
            return path;
        }
    }

    internal sealed record AssociationPathSet(
        string ExecutablePath,
        string StartMenuShortcut,
        string DesktopShortcut,
        string AppIdentityShortcut,
        string ProtocolCommand);

    internal sealed record ShortcutMetadata(
        string ExecutablePath,
        string Arguments,
        string Description,
        string AppUserModelId,
        string IconPath);

    internal interface IAssociationBackend
    {
        AssociationEntryState InspectShortcut(
            string path,
            string executablePath,
            string arguments);
        void WriteShortcut(
            string path,
            string executablePath,
            string arguments,
            string appUserModelId,
            bool replaceExisting);
        void DeleteShortcut(
            string path,
            string executablePath,
            string arguments);
        AssociationEntryState InspectProtocol(
            string commandKey,
            string executablePath);
        void WriteProtocol(string commandKey, string executablePath);
        void DeleteProtocol(string commandKey, string executablePath);
        IAssociationEntrySnapshot CaptureShortcutRollback(string path);
        IAssociationEntrySnapshot CaptureProtocolRollback(string commandKey);
    }

    internal interface IAssociationEntrySnapshot
    {
        void SealExpectedGeneration();
        void RestoreIfExpected();
    }

    internal sealed class WindowsAssociationBackend : IAssociationBackend
    {
        private const string CodexDescription = "Start Codex through MyCO";
        private const string IdentityDescription = "Open MyCO";
        private const string ProtocolMarker = "URL:MyCO Codex Launch";
        private const ushort VtLpwstr = 31;
        private readonly string[] _trustedShortcutRoots;

        public WindowsAssociationBackend()
            : this(
            [
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            ])
        {
        }

        internal WindowsAssociationBackend(IEnumerable<string> trustedShortcutRoots)
        {
            _trustedShortcutRoots = trustedShortcutRoots
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (_trustedShortcutRoots.Length == 0)
            {
                throw new InvalidOperationException(
                    "At least one trusted shortcut root is required.");
            }
        }

        public AssociationEntryState InspectShortcut(
            string path,
            string executablePath,
            string arguments)
        {
            FileAttributes attributes;
            try
            {
                EnsureSafeShortcutPath(path);
                if (!TryGetAttributes(path, out attributes))
                {
                    return AssociationEntryState.Missing;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                System.Security.SecurityException)
            {
                return AssociationEntryState.Foreign;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return AssociationEntryState.Foreign;
            }

            var metadata = ReadShortcutMetadata(path);
            if (metadata is null ||
                !string.Equals(metadata.Arguments, arguments, StringComparison.Ordinal) ||
                !TryNormalizeMyCOExecutable(
                    metadata.ExecutablePath,
                    out var shortcutExecutable))
            {
                return AssociationEntryState.Foreign;
            }

            var expectedDescription = arguments.Length == 0
                ? IdentityDescription
                : CodexDescription;
            var hasCurrentMarker = string.Equals(
                metadata.AppUserModelId,
                AppUserModelId,
                StringComparison.Ordinal);
            var hasLegacyMarker = string.IsNullOrEmpty(metadata.AppUserModelId) &&
                                  string.Equals(
                                      metadata.Description,
                                      expectedDescription,
                                      StringComparison.Ordinal);
            if (!hasCurrentMarker && !hasLegacyMarker)
            {
                return AssociationEntryState.Foreign;
            }

            // Shortcuts written before AUMID support are recognizable by the
            // exact MyCO description, but remain stale until rewritten with
            // the current identity and icon metadata.
            if (hasLegacyMarker)
            {
                return AssociationEntryState.StaleOwned;
            }

            return string.Equals(
                shortcutExecutable,
                executablePath,
                StringComparison.OrdinalIgnoreCase)
                ? AssociationEntryState.CurrentOwned
                : AssociationEntryState.StaleOwned;
        }

        public ShortcutMetadata? ReadShortcutMetadata(string path)
        {
            object? shellObject = null;
            try
            {
                shellObject = new ShellLink();
                var link = (IShellLinkW)shellObject;
                ((IPersistFile)shellObject).Load(path, 0);
                var target = new StringBuilder(32768);
                var arguments = new StringBuilder(2048);
                var description = new StringBuilder(1024);
                var icon = new StringBuilder(32768);
                link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
                link.GetArguments(arguments, arguments.Capacity);
                link.GetDescription(description, description.Capacity);
                link.GetIconLocation(icon, icon.Capacity, out _);
                return new ShortcutMetadata(
                    target.ToString(),
                    arguments.ToString(),
                    description.ToString(),
                    ReadAppUserModelId((IPropertyStore)shellObject),
                    icon.ToString());
            }
            catch (Exception exception) when (
                exception is COMException or ArgumentException or
                InvalidCastException)
            {
                return null;
            }
            finally
            {
                ReleaseComObject(shellObject);
            }
        }

        public void WriteShortcut(
            string path,
            string executablePath,
            string arguments,
            string appUserModelId,
            bool replaceExisting)
        {
            EnsureSafeShortcutPath(path);
            var expectedState = replaceExisting
                ? AssociationEntryState.StaleOwned
                : AssociationEntryState.Missing;
            if (InspectShortcut(path, executablePath, arguments) != expectedState)
            {
                throw new AssociationEntryChangedException();
            }
            var exists = TryGetAttributes(path, out var attributes);
            if (exists && (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("A reparse point cannot be replaced by MyCO.");
            }
            if (exists && !replaceExisting)
            {
                throw new IOException($"The shortcut already exists: {path}");
            }

            var directory = Path.GetDirectoryName(path)
                            ?? throw new IOException("Shortcut directory is missing.");
            Directory.CreateDirectory(directory);
            EnsureSafeShortcutPath(path);
            // The Shell property store persists AppUserModelID only when the
            // staged file is itself recognized as a .lnk.
            var temporary = $"{path}.{Guid.NewGuid():N}.tmp.lnk";
            try
            {
                object? shellObject = null;
                try
                {
                    shellObject = new ShellLink();
                    var link = (IShellLinkW)shellObject;
                    link.SetPath(executablePath);
                    link.SetArguments(arguments);
                    link.SetDescription(
                        arguments.Length == 0 ? IdentityDescription : CodexDescription);
                    link.SetWorkingDirectory(
                        Path.GetDirectoryName(executablePath) ?? string.Empty);
                    link.SetIconLocation(executablePath, 0);
                    WriteAppUserModelIdAndSave(
                        (IPropertyStore)shellObject,
                        (IPersistFile)shellObject,
                        appUserModelId,
                        temporary);
                }
                finally
                {
                    ReleaseComObject(shellObject);
                }
                EnsureSafeShortcutPath(path);
                if (InspectShortcut(path, executablePath, arguments) != expectedState)
                {
                    throw new AssociationEntryChangedException();
                }
                File.Move(temporary, path, overwrite: replaceExisting);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        public void DeleteShortcut(
            string path,
            string executablePath,
            string arguments)
        {
            EnsureSafeShortcutPath(path);
            var state = InspectShortcut(path, executablePath, arguments);
            if (state is not (AssociationEntryState.CurrentOwned or
                AssociationEntryState.StaleOwned))
            {
                throw new AssociationEntryChangedException();
            }
            if (!TryGetAttributes(path, out var attributes))
            {
                return;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("A reparse point cannot be deleted by MyCO.");
            }
            File.Delete(path);
        }

        public IAssociationEntrySnapshot CaptureShortcutRollback(string path)
        {
            EnsureSafeShortcutPath(path);
            return new ShortcutEntrySnapshot(this, path, CaptureFileGeneration(path));
        }

        public AssociationEntryState InspectProtocol(
            string commandKey,
            string executablePath)
        {
            try
            {
                var protocol = ProtocolRoot(commandKey);
                using var root = Registry.CurrentUser.OpenSubKey(protocol, writable: false);
                if (root is null)
                {
                    return AssociationEntryState.Missing;
                }
                using var command = Registry.CurrentUser.OpenSubKey(
                    commandKey,
                    writable: false);
                var hasUrlProtocol = root.GetValueNames().Any(name =>
                    string.Equals(name, "URL Protocol", StringComparison.Ordinal));
                if (!string.Equals(
                        root.GetValue(null) as string,
                        ProtocolMarker,
                        StringComparison.Ordinal) ||
                    !hasUrlProtocol ||
                    command?.GetValue(null) is not string commandValue ||
                    !TryParseProtocolCommand(commandValue, out var target))
                {
                    return AssociationEntryState.Foreign;
                }

                return string.Equals(
                    target,
                    executablePath,
                    StringComparison.OrdinalIgnoreCase)
                    ? AssociationEntryState.CurrentOwned
                    : AssociationEntryState.StaleOwned;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException)
            {
                return AssociationEntryState.Foreign;
            }
        }

        public void WriteProtocol(string commandKey, string executablePath)
        {
            var state = InspectProtocol(commandKey, executablePath);
            if (state is not (AssociationEntryState.Missing or
                AssociationEntryState.StaleOwned))
            {
                throw new AssociationEntryChangedException();
            }
            var protocol = ProtocolRoot(commandKey);
            using var protocolKey = Registry.CurrentUser.CreateSubKey(protocol)
                                    ?? throw new InvalidOperationException(
                                        "The per-user protocol key is unavailable.");
            protocolKey.SetValue(null, ProtocolMarker);
            protocolKey.SetValue("URL Protocol", string.Empty);
            using var command = Registry.CurrentUser.CreateSubKey(commandKey)
                                ?? throw new InvalidOperationException(
                                    "The per-user protocol command key is unavailable.");
            command.SetValue(null, BuildProtocolCommand(executablePath));
        }

        public void DeleteProtocol(string commandKey, string executablePath)
        {
            var state = InspectProtocol(commandKey, executablePath);
            if (state is not (AssociationEntryState.CurrentOwned or
                AssociationEntryState.StaleOwned))
            {
                throw new AssociationEntryChangedException();
            }
            var protocol = ProtocolRoot(commandKey);
            Registry.CurrentUser.DeleteSubKeyTree(
                commandKey,
                throwOnMissingSubKey: false);
            DeleteEmptySubKey(protocol + "\\shell\\open");
            DeleteEmptySubKey(protocol + "\\shell");

            using var root = Registry.CurrentUser.OpenSubKey(protocol, writable: false);
            if (root is null ||
                root.GetSubKeyNames().Length != 0 ||
                root.GetValueNames().Any(name =>
                    name.Length != 0 && !string.Equals(
                        name,
                        "URL Protocol",
                        StringComparison.Ordinal)))
            {
                return;
            }
            Registry.CurrentUser.DeleteSubKeyTree(
                protocol,
                throwOnMissingSubKey: false);
        }

        public IAssociationEntrySnapshot CaptureProtocolRollback(string commandKey)
        {
            var rootPath = ProtocolRoot(commandKey);
            return new ProtocolEntrySnapshot(rootPath, CaptureRegistryGeneration(rootPath));
        }

        private static bool TryParseProtocolCommand(
            string command,
            out string executablePath)
        {
            const string suffix = "\" --codex-launch \"%1\"";
            executablePath = string.Empty;
            if (command.Length <= suffix.Length + 1 ||
                command[0] != '\"' ||
                !command.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }
            var candidate = command[1..^suffix.Length];
            return TryNormalizeMyCOExecutable(candidate, out executablePath);
        }

        private static bool TryNormalizeMyCOExecutable(
            string candidate,
            out string executablePath)
        {
            executablePath = string.Empty;
            if (string.IsNullOrWhiteSpace(candidate) ||
                !string.Equals(
                    Path.GetFileName(candidate),
                    "MyCO.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            try
            {
                executablePath = Path.GetFullPath(candidate);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or
                PathTooLongException)
            {
                return false;
            }
        }

        private static bool TryGetAttributes(
            string path,
            out FileAttributes attributes)
        {
            try
            {
                attributes = File.GetAttributes(path);
                return true;
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or DirectoryNotFoundException)
            {
                attributes = default;
                return false;
            }
        }

        private void EnsureSafeShortcutPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var root = _trustedShortcutRoots
                .Where(candidate => IsWithinRoot(fullPath, candidate))
                .OrderByDescending(candidate => candidate.Length)
                .FirstOrDefault()
                ?? throw new IOException(
                    "The shortcut path is outside the trusted per-user roots.");

            EnsureExistingComponentIsNotReparse(root);
            var relative = Path.GetRelativePath(root, fullPath);
            var current = root;
            foreach (var component in relative.Split(
                         Path.DirectorySeparatorChar,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, component);
                EnsureExistingComponentIsNotReparse(current);
            }
        }

        private static bool IsWithinRoot(string path, string root) =>
            string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(
                root.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

        private static void EnsureExistingComponentIsNotReparse(string path)
        {
            if (!TryGetAttributes(path, out var attributes))
            {
                return;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "A reparse point is not allowed in an association path.");
            }
        }

        private FileGeneration CaptureFileGeneration(string path)
        {
            EnsureSafeShortcutPath(path);
            if (!TryGetAttributes(path, out var attributes))
            {
                return FileGeneration.Missing;
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException("A shortcut path cannot be a directory.");
            }
            return new FileGeneration(true, attributes, File.ReadAllBytes(path));
        }

        private void RestoreFileGeneration(
            string path,
            FileGeneration original,
            FileGeneration expected)
        {
            EnsureSafeShortcutPath(path);
            if (!CaptureFileGeneration(path).Matches(expected))
            {
                return;
            }
            if (!original.Exists)
            {
                DeleteNonReparseIfPresent(path);
                return;
            }

            var directory = Path.GetDirectoryName(path)
                            ?? throw new IOException("Shortcut directory is missing.");
            Directory.CreateDirectory(directory);
            EnsureSafeShortcutPath(path);
            var temporary = $"{path}.{Guid.NewGuid():N}.rollback.tmp";
            try
            {
                File.WriteAllBytes(temporary, original.Contents);
                EnsureSafeShortcutPath(path);
                if (!CaptureFileGeneration(path).Matches(expected))
                {
                    return;
                }
                File.Move(temporary, path, overwrite: true);
                File.SetAttributes(path, original.Attributes);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static RegistrySnapshot? CaptureRegistryGeneration(string rootPath)
        {
            using var root = Registry.CurrentUser.OpenSubKey(rootPath, writable: false);
            return root is null ? null : RegistrySnapshot.Capture(root);
        }

        private static void RestoreRegistryGeneration(
            string rootPath,
            RegistrySnapshot? original,
            RegistrySnapshot? expected)
        {
            var current = CaptureRegistryGeneration(rootPath);
            if (!RegistrySnapshot.Matches(current, expected))
            {
                return;
            }
            Registry.CurrentUser.DeleteSubKeyTree(
                rootPath,
                throwOnMissingSubKey: false);
            if (original is null)
            {
                return;
            }
            using var restored = Registry.CurrentUser.CreateSubKey(rootPath)
                                 ?? throw new InvalidOperationException(
                                     "The protocol rollback key is unavailable.");
            original.Restore(restored);
        }

        private static void DeleteNonReparseIfPresent(string path)
        {
            if (!TryGetAttributes(path, out var attributes))
            {
                return;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("A reparse point cannot be replaced by rollback.");
            }
            File.Delete(path);
        }

        private sealed class ShortcutEntrySnapshot : IAssociationEntrySnapshot
        {
            private readonly WindowsAssociationBackend _backend;
            private readonly string _path;
            private readonly FileGeneration _original;
            private FileGeneration _expected;

            public ShortcutEntrySnapshot(
                WindowsAssociationBackend backend,
                string path,
                FileGeneration original)
            {
                _backend = backend;
                _path = path;
                _original = original;
                _expected = original;
            }

            public void SealExpectedGeneration() =>
                _expected = _backend.CaptureFileGeneration(_path);

            public void RestoreIfExpected() =>
                _backend.RestoreFileGeneration(
                    _path,
                    _original,
                    _expected);
        }

        private sealed class ProtocolEntrySnapshot : IAssociationEntrySnapshot
        {
            private readonly string _rootPath;
            private readonly RegistrySnapshot? _original;
            private RegistrySnapshot? _expected;

            public ProtocolEntrySnapshot(
                string rootPath,
                RegistrySnapshot? original)
            {
                _rootPath = rootPath;
                _original = original;
                _expected = original;
            }

            public void SealExpectedGeneration() =>
                _expected = CaptureRegistryGeneration(_rootPath);

            public void RestoreIfExpected() =>
                RestoreRegistryGeneration(
                    _rootPath,
                    _original,
                    _expected);
        }

        private sealed record FileGeneration(
            bool Exists,
            FileAttributes Attributes,
            byte[] Contents)
        {
            public static FileGeneration Missing { get; } =
                new(false, default, []);

            public bool Matches(FileGeneration other) =>
                Exists == other.Exists &&
                (!Exists ||
                 (Attributes == other.Attributes &&
                  Contents.AsSpan().SequenceEqual(other.Contents)));
        }

        private static string ReadAppUserModelId(IPropertyStore store)
        {
            var key = AppUserModelIdKey;
            store.GetValue(ref key, out var value);
            try
            {
                return value.VariantType == VtLpwstr && value.Pointer != IntPtr.Zero
                    ? Marshal.PtrToStringUni(value.Pointer) ?? string.Empty
                    : string.Empty;
            }
            finally
            {
                _ = PropVariantClear(ref value);
            }
        }

        private static void WriteAppUserModelIdAndSave(
            IPropertyStore store,
            IPersistFile persist,
            string appUserModelId,
            string path)
        {
            var key = AppUserModelIdKey;
            var value = new PropVariant
            {
                VariantType = VtLpwstr,
                Pointer = Marshal.StringToCoTaskMemUni(appUserModelId)
            };
            try
            {
                store.SetValue(ref key, ref value);
                store.Commit();
                // Keep the PROPVARIANT storage alive until the shell link has
                // serialized its property store.
                persist.Save(path, true);
            }
            finally
            {
                Marshal.FreeCoTaskMem(value.Pointer);
            }
        }

        private static void ReleaseComObject(object? value)
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                _ = Marshal.FinalReleaseComObject(value);
            }
        }

        private static string ProtocolRoot(string commandKey) =>
            commandKey[..commandKey.IndexOf("\\shell", StringComparison.Ordinal)];

        private static void DeleteEmptySubKey(string path)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(path, throwOnMissingSubKey: false);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException)
            {
                // A user-owned child keeps the parent; never widen the delete.
            }
        }

        private static string BuildProtocolCommand(string executablePath) =>
            $"\"{executablePath}\" --codex-launch \"%1\"";

        private static PropertyKey AppUserModelIdKey => new()
        {
            FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            PropertyId = 5
        };

        private sealed record RegistryValue(
            string Name,
            object Value,
            RegistryValueKind Kind);

        private sealed record RegistrySnapshot(
            IReadOnlyList<RegistryValue> Values,
            IReadOnlyDictionary<string, RegistrySnapshot> Children)
        {
            public static bool Matches(
                RegistrySnapshot? left,
                RegistrySnapshot? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return true;
                }
                if (left is null || right is null ||
                    left.Values.Count != right.Values.Count ||
                    left.Children.Count != right.Children.Count)
                {
                    return false;
                }
                var rightValues = right.Values.ToDictionary(
                    value => value.Name,
                    StringComparer.OrdinalIgnoreCase);
                foreach (var value in left.Values)
                {
                    if (!rightValues.TryGetValue(value.Name, out var candidate) ||
                        value.Kind != candidate.Kind ||
                        !RegistryValueEquals(value.Value, candidate.Value))
                    {
                        return false;
                    }
                }
                foreach (var child in left.Children)
                {
                    if (!right.Children.TryGetValue(child.Key, out var candidate) ||
                        !Matches(child.Value, candidate))
                    {
                        return false;
                    }
                }
                return true;
            }

            private static bool RegistryValueEquals(object left, object right)
            {
                if (left is byte[] leftBytes && right is byte[] rightBytes)
                {
                    return leftBytes.AsSpan().SequenceEqual(rightBytes);
                }
                if (left is string[] leftStrings && right is string[] rightStrings)
                {
                    return leftStrings.SequenceEqual(rightStrings, StringComparer.Ordinal);
                }
                return Equals(left, right);
            }

            public static RegistrySnapshot Capture(RegistryKey key)
            {
                var values = key.GetValueNames()
                    .Select(name => new RegistryValue(
                        name,
                        key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                        ?? string.Empty,
                        key.GetValueKind(name)))
                    .ToArray();
                var children = new Dictionary<string, RegistrySnapshot>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var name in key.GetSubKeyNames())
                {
                    using var child = key.OpenSubKey(name, writable: false)
                                      ?? throw new IOException(
                                          "A protocol subkey disappeared during snapshot.");
                    children[name] = Capture(child);
                }
                return new RegistrySnapshot(values, children);
            }

            public void Restore(RegistryKey key)
            {
                foreach (var value in Values)
                {
                    key.SetValue(value.Name, value.Value, value.Kind);
                }
                foreach (var child in Children)
                {
                    using var childKey = key.CreateSubKey(child.Key)
                                         ?? throw new IOException(
                                             "A protocol subkey could not be restored.");
                    child.Value.Restore(childKey);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public IntPtr Pointer;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [Out] StringBuilder pszFile,
            int cch,
            IntPtr pfd,
            uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

}

public static class MyCOAppIdentity
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void Apply()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        _ = SetCurrentProcessExplicitAppUserModelID(
            CodexLaunchAssociationService.AppUserModelId);
    }
}
