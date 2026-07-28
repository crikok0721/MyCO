using Microsoft.Win32;

namespace MyCodex.Startup;

// Owns only MyCodex's per-user Run value; it never touches HKLM or other entries.
public sealed class StartupRegistrationService : IStartupRegistrationService
{
    internal const string ProductionValueName = "MyCodex";
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IRunKeyBackend _backend;
    private readonly string _valueName;

    public StartupRegistrationService()
        : this(new RegistryRunKeyBackend(), ProductionValueName)
    {
    }

    internal StartupRegistrationService(
        IRunKeyBackend backend,
        string valueName)
    {
        _backend = backend;
        _valueName = string.IsNullOrWhiteSpace(valueName)
            ? throw new ArgumentException("Startup value name is required.", nameof(valueName))
            : valueName;
    }

    public StartupRegistrationStatus GetStatus(string executablePath)
    {
        var expected = BuildCommand(executablePath);
        var registered = _backend.Read(_valueName);
        return new StartupRegistrationStatus(
            registered is not null,
            string.Equals(registered, expected, StringComparison.Ordinal),
            registered);
    }

    public void SetEnabled(string executablePath, bool enabled)
    {
        if (enabled)
        {
            _backend.Write(_valueName, BuildCommand(executablePath));
            var verified = _backend.Read(_valueName);
            if (!string.Equals(verified, BuildCommand(executablePath), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The MyCodex startup registration could not be verified.");
            }
            return;
        }

        _backend.Delete(_valueName);
        if (_backend.Read(_valueName) is not null)
        {
            throw new InvalidOperationException(
                "The MyCodex startup registration could not be removed.");
        }
    }

    public void Restore(StartupRegistrationStatus status)
    {
        if (status.IsRegistered && status.RegisteredCommand is not null)
        {
            _backend.Write(_valueName, status.RegisteredCommand);
            if (!string.Equals(
                    _backend.Read(_valueName),
                    status.RegisteredCommand,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The prior MyCodex startup registration could not be restored.");
            }
            return;
        }
        _backend.Delete(_valueName);
    }

    internal static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }
        var fullPath = Path.GetFullPath(executablePath);
        if (!fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Startup target must be an executable.", nameof(executablePath));
        }
        if (fullPath.Contains('"'))
        {
            throw new ArgumentException("Executable path contains an invalid quote.", nameof(executablePath));
        }
        return $"\"{fullPath}\" --background";
    }

    internal interface IRunKeyBackend
    {
        string? Read(string valueName);
        void Write(string valueName, string command);
        void Delete(string valueName);
    }

    internal sealed class RegistryRunKeyBackend : IRunKeyBackend
    {
        public string? Read(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                writable: false);
            return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                as string;
        }

        public void Write(string valueName, string command)
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                RunKeyPath,
                writable: true)
                ?? throw new InvalidOperationException(
                    "The per-user Windows startup registry key is unavailable.");
            key.SetValue(
                valueName,
                command,
                RegistryValueKind.String);
        }

        public void Delete(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
