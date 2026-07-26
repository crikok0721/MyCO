using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyCodex.Compatibility;

// Loads, validates, migrates, and atomically saves the user's local configuration.
namespace MyCodex.Configuration;

public sealed class ConfigStore
{
    private const long MaximumConfigurationBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ConfigPaths _paths;

    public ConfigStore(ConfigPaths? paths = null)
    {
        _paths = paths ?? new ConfigPaths();
    }

    public async Task<ConfigLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        if (!File.Exists(_paths.ConfigFile))
        {
            var defaults = AppConfig.Default;
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return new ConfigLoadResult(defaults, true, false, null);
        }

        try
        {
            EnsureFileSize(_paths.ConfigFile);
            var json = await File.ReadAllTextAsync(_paths.ConfigFile, cancellationToken)
                .ConfigureAwait(false);
            var node = JsonNode.Parse(json)?.AsObject()
                       ?? throw new JsonException("Configuration root must be an object.");
            // Missing or older schema values are handled by the narrow legacy migrator below.
            var schemaVersion = node["schemaVersion"]?.GetValue<int>() ?? 0;
            var migrated = schemaVersion != BuildInfo.ConfigSchemaVersion;
            var config = migrated ? ConfigMigration.Migrate(node) : Deserialize(node);
            if (!LanguageCodes.IsSupported(config.Language))
            {
                config = config with { Language = LanguageCodes.English };
                migrated = true;
            }
            config = config with
            {
                Calibration = await LoadCalibrationAsync(
                    config.Calibration,
                    cancellationToken).ConfigureAwait(false)
            };
            config = Normalize(config);
            if (migrated)
            {
                await SaveAsync(config, cancellationToken).ConfigureAwait(false);
            }
            return new ConfigLoadResult(config, false, migrated, null);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            // Preserve the bad file for diagnosis, then recover with known-safe defaults.
            var backup = PreserveCorruptFile(_paths.ConfigFile, "config");
            var defaults = AppConfig.Default;
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return new ConfigLoadResult(defaults, false, false, backup);
        }
    }

    public async Task SaveAsync(
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        var normalized = Normalize(config);
        // Write-then-move avoids leaving a half-written JSON file after a crash.
        var temporary = TemporaryPath(_paths.ConfigFile);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await WriteAtomicAsync(
            _paths.ConfigFile,
            temporary,
            json,
            cancellationToken).ConfigureAwait(false);

        var calibrationTemporary = TemporaryPath(_paths.CalibrationFile);
        var calibrationJson = JsonSerializer.Serialize(normalized.Calibration, JsonOptions);
        await WriteAtomicAsync(
            _paths.CalibrationFile,
            calibrationTemporary,
            calibrationJson,
            cancellationToken).ConfigureAwait(false);
        PruneBackups();
    }

    private async Task<CalibrationConfig> LoadCalibrationAsync(
        CalibrationConfig fallback,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.CalibrationFile))
        {
            return fallback;
        }

        try
        {
            EnsureFileSize(_paths.CalibrationFile);
            var json = await File.ReadAllTextAsync(
                _paths.CalibrationFile,
                cancellationToken).ConfigureAwait(false);
            var calibration = JsonSerializer.Deserialize<CalibrationConfig>(json, JsonOptions)
                              ?? throw new JsonException(
                                  "Calibration configuration could not be deserialized.");
            if (calibration.SchemaVersion != BuildInfo.CalibrationSchemaVersion)
            {
                throw new InvalidOperationException("Unsupported calibration schema.");
            }
            return NormalizeCalibration(calibration);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            PreserveCorruptFile(_paths.CalibrationFile, "calibration");
            var temporary = TemporaryPath(_paths.CalibrationFile);
            var json = JsonSerializer.Serialize(fallback, JsonOptions);
            await WriteAtomicAsync(
                _paths.CalibrationFile,
                temporary,
                json,
                cancellationToken).ConfigureAwait(false);
            return fallback;
        }
    }

    private static AppConfig Deserialize(JsonNode node)
    {
        return node.Deserialize<AppConfig>(JsonOptions)
               ?? throw new JsonException("Configuration could not be deserialized.");
    }

    private static AppConfig Normalize(AppConfig config)
    {
        if (config.SchemaVersion != BuildInfo.ConfigSchemaVersion ||
            config.ProtocolVersion != BuildInfo.ProtocolVersion ||
            config.Calibration.SchemaVersion != BuildInfo.CalibrationSchemaVersion)
        {
            throw new InvalidOperationException("Unsupported configuration schema.");
        }
        if (config.Appearance.AvatarSize is < 24 or > 96 ||
            config.Appearance.BubbleRadius is < 0 or > 36 ||
            config.Appearance.BubblePaddingX is < 4 or > 40 ||
            config.Appearance.BubblePaddingY is < 4 or > 32 ||
            config.Appearance.MessageGap is < 4 or > 80 ||
            config.Appearance.MessageMaxWidth is < 35 or > 90)
        {
            throw new ArgumentException("Appearance values are outside supported ranges.");
        }
        if (config.Appearance.Preset is not ("ReferenceDark" or "Minimal"))
        {
            throw new ArgumentException("Appearance preset is not supported.");
        }
        if ((config.Assistant.Avatar?.Length ?? 0) > 1024 ||
            (config.User.Avatar?.Length ?? 0) > 1024)
        {
            throw new ArgumentException("Avatar paths are too long.");
        }
        foreach (var color in new[]
                 {
                     config.Appearance.UserBubble,
                     config.Appearance.AssistantBubble,
                     config.Appearance.UserText,
                     config.Appearance.AssistantText,
                     config.Appearance.NicknameColor
                 })
        {
            if (!Regex.IsMatch(
                    color ?? string.Empty,
                    "^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$",
                    RegexOptions.CultureInvariant))
            {
                throw new ArgumentException("Appearance colors must be hexadecimal values.");
            }
        }

        return config with
        {
            Language = LanguageCodes.Normalize(config.Language),
            Assistant = config.Assistant with
            {
                Name = NicknameValidator.Normalize(config.Assistant.Name)
            },
            User = config.User with
            {
                Name = NicknameValidator.Normalize(config.User.Name)
            },
            Calibration = NormalizeCalibration(config.Calibration)
        };
    }

    private static CalibrationConfig NormalizeCalibration(CalibrationConfig calibration)
    {
        return calibration with
        {
            UserTurn = calibration.UserTurn is null
                ? null
                : ElementSignatureValidator.Normalize(calibration.UserTurn),
            AssistantTurn = calibration.AssistantTurn is null
                ? null
                : ElementSignatureValidator.Normalize(calibration.AssistantTurn)
        };
    }

    private static void EnsureFileSize(string path)
    {
        if (new FileInfo(path).Length > MaximumConfigurationBytes)
        {
            throw new InvalidOperationException("Configuration file is too large.");
        }
    }

    private static string TemporaryPath(string destination)
    {
        return $"{destination}.{Guid.NewGuid():N}.tmp";
    }

    private string PreserveCorruptFile(string source, string kind)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var backup = Path.Combine(
            _paths.BackupsDirectory,
            $"{kind}.corrupt-{timestamp}-{Guid.NewGuid():N}.json");
        // Move instead of copy so an oversized hostile file cannot double disk usage.
        File.Move(source, backup);
        return backup;
    }

    private static async Task WriteAtomicAsync(
        string destination,
        string temporary,
        string content,
        CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private void PruneBackups()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(30);
        var files = new DirectoryInfo(_paths.BackupsDirectory)
            .EnumerateFiles("*.json")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
        for (var index = 0; index < files.Length; index++)
        {
            if (index >= 10 || files[index].LastWriteTimeUtc < cutoff.UtcDateTime)
            {
                files[index].Delete();
            }
        }
    }
}

internal static class ConfigMigration
{
    public static AppConfig Migrate(JsonObject source)
    {
        // Version 0 used flat person fields; version 1 stores nested person objects.
        var assistantName =
            source["assistantName"]?.GetValue<string>() ??
            source["assistant"]?["name"]?.GetValue<string>() ??
            "Codex";
        var assistantAvatar =
            source["assistantAvatar"]?.GetValue<string>() ??
            source["assistant"]?["avatar"]?.GetValue<string>() ??
            string.Empty;
        var userName =
            source["userName"]?.GetValue<string>() ??
            source["user"]?["name"]?.GetValue<string>() ??
            "You";
        var userAvatar =
            source["userAvatar"]?.GetValue<string>() ??
            source["user"]?["avatar"]?.GetValue<string>() ??
            string.Empty;

        return AppConfig.Default with
        {
            Assistant = new PersonConfig
            {
                Name = assistantName,
                Avatar = assistantAvatar
            },
            User = new PersonConfig
            {
                Name = userName,
                Avatar = userAvatar
            }
        };
    }
}
