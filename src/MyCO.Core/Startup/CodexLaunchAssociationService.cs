using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace MyCO.Startup;

public sealed record CodexLaunchAssociationStatus(
    bool IsEnabled,
    string StartMenuShortcut,
    string DesktopShortcut,
    string ProtocolCommand);

// Registers only MyCO-owned launch surfaces. It never rewrites an existing
// official Codex shortcut or protocol registration that belongs to another app.
public sealed class CodexLaunchAssociationService
{
    public const string ProtocolScheme = "myco-codex";
    public const string AppUserModelId = "Crikok.MyCO";

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
            _backend.IsOwnedShortcut(paths.StartMenuShortcut, paths.ExecutablePath) &&
            _backend.IsOwnedShortcut(paths.DesktopShortcut, paths.ExecutablePath) &&
            _backend.IsOwnedProtocol(paths.ProtocolCommand, paths.ExecutablePath),
            paths.StartMenuShortcut,
            paths.DesktopShortcut,
            paths.ProtocolCommand);
    }

    public void SetEnabled(string executablePath, bool enabled)
    {
        var paths = AssociationPaths.For(executablePath);
        if (!enabled)
        {
            _backend.RemoveOwnedShortcut(paths.StartMenuShortcut, paths.ExecutablePath);
            _backend.RemoveOwnedShortcut(paths.DesktopShortcut, paths.ExecutablePath);
            _backend.RemoveOwnedProtocol(
                paths.ProtocolCommand,
                paths.ExecutablePath);
            return;
        }

        var created = new List<string>();
        var protocolCreated = false;
        try
        {
            foreach (var shortcut in new[]
                     {
                         paths.StartMenuShortcut,
                         paths.DesktopShortcut
                     })
            {
                if (!_backend.IsOwnedShortcut(shortcut, paths.ExecutablePath))
                {
                    _backend.EnsureShortcutAvailable(shortcut);
                    _backend.WriteShortcut(
                        shortcut,
                        paths.ExecutablePath,
                        "--codex-launch",
                        AppUserModelId);
                    created.Add(shortcut);
                }
            }

            if (!_backend.IsOwnedProtocol(paths.ProtocolCommand, paths.ExecutablePath))
            {
                _backend.EnsureProtocolAvailable(
                    paths.ProtocolCommand,
                    paths.ExecutablePath);
                _backend.WriteProtocol(paths.ProtocolCommand, paths.ExecutablePath);
                protocolCreated = true;
            }
        }
        catch
        {
            foreach (var shortcut in created)
            {
                _backend.RemoveOwnedShortcut(shortcut, paths.ExecutablePath);
            }
            if (protocolCreated)
            {
                _backend.RemoveOwnedProtocol(
                    paths.ProtocolCommand,
                    paths.ExecutablePath);
            }
            throw;
        }
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

            var startMenu = Path.Combine(
                RequiredFolder(Environment.SpecialFolder.StartMenu),
                "Programs",
                "MyCO",
                "MyCO - Codex.lnk");
            var desktop = Path.Combine(
                RequiredFolder(Environment.SpecialFolder.DesktopDirectory),
                "MyCO - Codex.lnk");
            var protocol =
                $"Software\\Classes\\{ProtocolScheme}\\shell\\open\\command";
            return new AssociationPathSet(executable, startMenu, desktop, protocol);
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
        string ProtocolCommand);

    internal interface IAssociationBackend
    {
        bool IsOwnedShortcut(string path, string executablePath);
        void EnsureShortcutAvailable(string path);
        void WriteShortcut(
            string path,
            string executablePath,
            string arguments,
            string appUserModelId);
        void RemoveOwnedShortcut(string path, string executablePath);
        bool IsOwnedProtocol(string commandKey, string executablePath);
        void EnsureProtocolAvailable(string commandKey, string executablePath);
        void WriteProtocol(string commandKey, string executablePath);
        void RemoveOwnedProtocol(string commandKey, string executablePath);
    }

    private sealed class WindowsAssociationBackend : IAssociationBackend
    {
        public bool IsOwnedShortcut(string path, string executablePath)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            try
            {
                var link = (IShellLinkW)(object)new ShellLink();
                var persist = (IPersistFile)link;
                try
                {
                    persist.Load(path, 0);
                    var target = new StringBuilder(32768);
                    var arguments = new StringBuilder(2048);
                    link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
                    link.GetArguments(arguments, arguments.Capacity);
                    var targetPath = target.ToString();
                    if (string.IsNullOrWhiteSpace(targetPath))
                    {
                        return false;
                    }

                    return string.Equals(
                               Path.GetFullPath(targetPath),
                               executablePath,
                               StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(
                               arguments.ToString(),
                               "--codex-launch",
                               StringComparison.Ordinal);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(persist);
                    Marshal.FinalReleaseComObject(link);
                }
            }
            catch (COMException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                // A malformed shortcut must never abort MyCO startup.
                return false;
            }
        }

        public void EnsureShortcutAvailable(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            throw new IOException(
                $"The shortcut is owned by another application: {path}");
        }

        public void WriteShortcut(
            string path,
            string executablePath,
            string arguments,
            string appUserModelId)
        {
            _ = appUserModelId;
            var directory = Path.GetDirectoryName(path)
                            ?? throw new IOException("Shortcut directory is missing.");
            Directory.CreateDirectory(directory);
            var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                var link = (IShellLinkW)(object)new ShellLink();
                link.SetPath(executablePath);
                link.SetArguments(arguments);
                link.SetDescription("Start Codex through MyCO");
                link.SetWorkingDirectory(Path.GetDirectoryName(executablePath) ?? string.Empty);
                var persist = (IPersistFile)link;
                persist.Save(temporary, true);
                Marshal.FinalReleaseComObject(persist);
                Marshal.FinalReleaseComObject(link);
                File.Move(temporary, path, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        public void RemoveOwnedShortcut(string path, string executablePath)
        {
            if (IsOwnedShortcut(path, executablePath))
            {
                File.Delete(path);
            }
        }

        public bool IsOwnedProtocol(string commandKey, string executablePath)
        {
            var protocol = ProtocolRoot(commandKey);
            using var root = Registry.CurrentUser.OpenSubKey(protocol, writable: false);
            using var command = Registry.CurrentUser.OpenSubKey(commandKey, writable: false);
            var marker = root?.GetValue(null) as string;
            var urlProtocol = root?.GetValue("URL Protocol") as string;
            var commandValue = command?.GetValue(null) as string;
            return string.Equals(
                       marker,
                       "URL:MyCO Codex Launch",
                       StringComparison.Ordinal) &&
                   urlProtocol is not null &&
                   string.Equals(
                       commandValue,
                       BuildProtocolCommand(executablePath),
                       StringComparison.Ordinal);
        }

        public void EnsureProtocolAvailable(string commandKey, string executablePath)
        {
            using var root = Registry.CurrentUser.OpenSubKey(
                ProtocolRoot(commandKey),
                writable: false);
            if (root is not null && !IsOwnedProtocol(commandKey, executablePath))
            {
                throw new IOException(
                    "The MyCO protocol is already registered by another application.");
            }
        }

        public void WriteProtocol(string commandKey, string executablePath)
        {
            var protocol = ProtocolRoot(commandKey);
            using var existing = Registry.CurrentUser.OpenSubKey(
                protocol,
                writable: false);
            var existed = existing is not null;
            try
            {
                using var protocolKey = Registry.CurrentUser.CreateSubKey(protocol)
                                        ?? throw new InvalidOperationException(
                                            "The per-user protocol key is unavailable.");
                protocolKey.SetValue(null, "URL:MyCO Codex Launch");
                protocolKey.SetValue("URL Protocol", string.Empty);
                using var command = Registry.CurrentUser.CreateSubKey(commandKey)
                                    ?? throw new InvalidOperationException(
                                        "The per-user protocol command key is unavailable.");
                command.SetValue(
                    null,
                    BuildProtocolCommand(executablePath));
            }
            catch
            {
                if (!existed)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(
                        protocol,
                        throwOnMissingSubKey: false);
                }
                throw;
            }
        }

        public void RemoveOwnedProtocol(string commandKey, string executablePath)
        {
            if (!IsOwnedProtocol(commandKey, executablePath))
            {
                return;
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

        private static string ProtocolRoot(string commandKey) =>
            commandKey[..commandKey.IndexOf("\\shell", StringComparison.Ordinal)];

        private static void DeleteEmptySubKey(string path)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(path, throwOnMissingSubKey: false);
            }
            catch (ArgumentException)
            {
                // A user-owned child keeps the parent; never widen the delete.
            }
            catch (InvalidOperationException)
            {
                // A user-owned child keeps the parent; never widen the delete.
            }
        }

        private static string BuildProtocolCommand(string executablePath) =>
            $"\"{executablePath}\" --codex-launch \"%1\"";
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
