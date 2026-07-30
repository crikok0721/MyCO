using Microsoft.Win32;

namespace MyCO.Startup;

// Owns only MyCO's per-user Run value and migrates known prior brand values.
public sealed class StartupRegistrationService : IStartupRegistrationService
{
    internal const string ProductionValueName = "MyCO";
    internal const string TransitionalProductionValueName = "Myco";
    internal const string LegacyProductionValueName = "MyCodex";
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IRunKeyBackend _backend;
    private readonly string _valueName;
    private readonly IReadOnlyList<string> _legacyValueNames;

    public StartupRegistrationService()
        : this(
            new RegistryRunKeyBackend(),
            ProductionValueName,
            TransitionalProductionValueName,
            LegacyProductionValueName)
    {
    }

    internal StartupRegistrationService(
        IRunKeyBackend backend,
        string valueName,
        params string?[] legacyValueNames)
    {
        _backend = backend;
        _valueName = string.IsNullOrWhiteSpace(valueName)
            ? throw new ArgumentException("Startup value name is required.", nameof(valueName))
            : valueName;
        _legacyValueNames = legacyValueNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public StartupRegistrationStatus GetStatus(string executablePath)
    {
        var expected = BuildCommand(executablePath);
        var registered = _backend.ExistsExact(_valueName)
            ? _backend.Read(_valueName)
            : null;
        foreach (var legacyValueName in _legacyValueNames)
        {
            if (registered is null && _backend.ExistsExact(legacyValueName))
            {
                registered = _backend.Read(legacyValueName);
            }
        }
        return new StartupRegistrationStatus(
            registered is not null,
            _backend.ExistsExact(_valueName) &&
            string.Equals(registered, expected, StringComparison.Ordinal),
            registered);
    }

    public void SetEnabled(string executablePath, bool enabled)
    {
        if (enabled)
        {
            var command = BuildCommand(executablePath);
            var caseOnlyLegacy = _legacyValueNames.FirstOrDefault(
                name => !string.Equals(name, _valueName, StringComparison.Ordinal) &&
                        string.Equals(name, _valueName, StringComparison.OrdinalIgnoreCase) &&
                        _backend.ExistsExact(name));
            var caseOnlyLegacyCommand = caseOnlyLegacy is null
                ? null
                : _backend.Read(caseOnlyLegacy);
            if (caseOnlyLegacy is not null)
            {
                _backend.Delete(caseOnlyLegacy);
            }
            try
            {
                _backend.Write(_valueName, command);
                var verified = _backend.ExistsExact(_valueName)
                    ? _backend.Read(_valueName)
                    : null;
                if (!string.Equals(verified, command, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The MyCO startup registration could not be verified.");
                }
                DeleteLegacyValuesAfterWrite();
            }
            catch
            {
                _backend.Delete(_valueName);
                if (caseOnlyLegacy is not null && caseOnlyLegacyCommand is not null)
                {
                    _backend.Write(caseOnlyLegacy, caseOnlyLegacyCommand);
                }
                throw;
            }
            return;
        }

        _backend.Delete(_valueName);
        DeleteAllLegacyValues();
        if (_backend.ExistsExact(_valueName) ||
            _legacyValueNames.Any(_backend.ExistsExact))
        {
            throw new InvalidOperationException(
                "The MyCO startup registration could not be removed.");
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
                    "The prior MyCO startup registration could not be restored.");
            }
            DeleteLegacyValuesAfterWrite();
            return;
        }
        _backend.Delete(_valueName);
        DeleteAllLegacyValues();
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

    private void DeleteLegacyValuesAfterWrite()
    {
        foreach (var legacyValueName in _legacyValueNames)
        {
            if (!string.Equals(
                    legacyValueName,
                    _valueName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _backend.Delete(legacyValueName);
            }
        }
    }

    private void DeleteAllLegacyValues()
    {
        foreach (var legacyValueName in _legacyValueNames)
        {
            _backend.Delete(legacyValueName);
        }
    }

    internal interface IRunKeyBackend
    {
        bool ExistsExact(string valueName);
        string? Read(string valueName);
        void Write(string valueName, string command);
        void Delete(string valueName);
    }

    internal sealed class RegistryRunKeyBackend : IRunKeyBackend
    {
        public bool ExistsExact(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                RunKeyPath,
                writable: false);
            return key?.GetValueNames().Contains(
                valueName,
                StringComparer.Ordinal) == true;
        }

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
