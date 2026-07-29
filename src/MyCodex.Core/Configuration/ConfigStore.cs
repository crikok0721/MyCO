using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MyCodex.Compatibility;

// Loads, validates, migrates, and atomically saves the user's local configuration.
namespace MyCodex.Configuration;

public sealed class ConfigStore
{
    private const long MaximumConfigurationBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
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
            var schemaVersion = node["schemaVersion"]?.GetValue<int>() ?? 0;
            var migration = ConfigMigration.Migrate(node, schemaVersion);
            var migrated = migration.WasMigrated;
            var config = migration.Config;
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
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            PreserveCorruptFile(_paths.CalibrationFile, "calibration");
            var clean = new CalibrationConfig();
            var temporary = TemporaryPath(_paths.CalibrationFile);
            var json = JsonSerializer.Serialize(clean, JsonOptions);
            await WriteAtomicAsync(
                _paths.CalibrationFile,
                temporary,
                json,
                cancellationToken).ConfigureAwait(false);
            return clean;
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
            config.Appearance.AvatarOffsetX is < -32 or > 32 ||
            config.Appearance.AvatarOffsetY is < -20 or > 40 ||
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
        if (!Enum.IsDefined(config.Appearance.BubbleDisplayMode))
        {
            throw new ArgumentException("Bubble display mode is not supported.");
        }
        if ((config.Assistant.Avatar?.Length ?? 0) > 1024 ||
            (config.User.Avatar?.Length ?? 0) > 1024)
        {
            throw new ArgumentException("Avatar paths are too long.");
        }
        foreach (var color in PaletteColors(config.Appearance.DarkBubblePalette)
                     .Concat(PaletteColors(config.Appearance.LightBubblePalette))
                     .Append(config.Appearance.UserBubble)
                     .Append(config.Appearance.UserText))
        {
            if (!Regex.IsMatch(
                    color ?? string.Empty,
                    "^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$",
                    RegexOptions.CultureInvariant))
            {
                throw new ArgumentException("Appearance colors must be hexadecimal values.");
            }
        }
        EnsureReadablePalette(
            config.Appearance.DarkBubblePalette,
            "#111214",
            "Dark");
        EnsureReadablePalette(
            config.Appearance.LightBubblePalette,
            "#FFFFFF",
            "Light");

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
        // Schema-1 single-click signatures did not contain sample/context proof.
        // Invalidate only that calibration data; names, avatars and appearance stay intact.
        var normalized = calibration with
        {
            UserTurn = !ElementSignatureValidator.IsValidatedMultiSample(
                calibration.UserTurn)
                ? null
                : ElementSignatureValidator.Normalize(calibration.UserTurn!),
            AssistantTurn = !ElementSignatureValidator.IsValidatedMultiSample(
                calibration.AssistantTurn)
                ? null
                : ElementSignatureValidator.Normalize(calibration.AssistantTurn!)
        };
        if (!ElementSignatureValidator.AreDistinctRoles(
                normalized.UserTurn,
                normalized.AssistantTurn))
        {
            throw new ArgumentException(
                "User and assistant calibration signatures are ambiguous.");
        }
        return normalized;
    }

    private static IEnumerable<string> PaletteColors(BubblePalette palette)
    {
        yield return palette.AssistantBubble;
        yield return palette.AssistantText;
        yield return palette.NicknameColor;
        yield return palette.AvatarBackground;
        yield return palette.AvatarBorder;
    }

    private static void EnsureReadablePalette(
        BubblePalette palette,
        string hostBackground,
        string name)
    {
        var ratio = ColorContrast.Calculate(
            palette.AssistantText,
            palette.AssistantBubble,
            hostBackground);
        if (ratio < 4.5)
        {
            throw new ArgumentException(
                $"{name} assistant text contrast must be at least 4.5:1.");
        }
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
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    public static ConfigMigrationResult Migrate(JsonObject source, int schemaVersion)
    {
        if (schemaVersion > BuildInfo.ConfigSchemaVersion)
        {
            throw new InvalidOperationException("Configuration schema is newer than MyCodex.");
        }

        var migrated = (JsonObject)source.DeepClone();
        var changed = schemaVersion != BuildInfo.ConfigSchemaVersion;

        if (schemaVersion == 0)
        {
            MigrateSchemaZero(migrated);
            changed = true;
        }

        var appearance = migrated["appearance"] as JsonObject ?? new JsonObject();
        if (migrated["appearance"] is not JsonObject)
        {
            migrated["appearance"] = appearance;
            changed = true;
        }
        if (appearance["darkBubblePalette"] is null)
        {
            appearance["darkBubblePalette"] = BuildLegacyDarkPalette(appearance);
            changed = true;
        }
        if (appearance["lightBubblePalette"] is null)
        {
            appearance["lightBubblePalette"] = JsonSerializer.SerializeToNode(
                BubblePalette.LightDefault,
                JsonOptions);
            changed = true;
        }
        changed |= EnsureValue(
            appearance,
            "bubbleDisplayMode",
            JsonValue.Create(BubbleDisplayMode.Automatic.ToString()));

        changed |= EnsureValue(
            migrated,
            "managerThemeMode",
            JsonValue.Create(ManagerThemeMode.System.ToString()));
        changed |= EnsureValue(migrated, "launchAtLogin", JsonValue.Create(false));
        changed |= EnsureValue(
            migrated,
            "launchCodexOnMyCodexStart",
            JsonValue.Create(false));
        migrated["schemaVersion"] = BuildInfo.ConfigSchemaVersion;
        migrated["protocolVersion"] = BuildInfo.ProtocolVersion;

        var config = migrated.Deserialize<AppConfig>(JsonOptions)
                     ?? throw new JsonException(
                         "Migrated configuration could not be deserialized.");
        return new ConfigMigrationResult(config, changed);
    }

    private static void MigrateSchemaZero(JsonObject source)
    {
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

        source["assistant"] = JsonSerializer.SerializeToNode(
            new PersonConfig
            {
                Name = assistantName,
                Avatar = assistantAvatar
            },
            JsonOptions);
        source["user"] = JsonSerializer.SerializeToNode(
            new PersonConfig
            {
                Name = userName,
                Avatar = userAvatar
            },
            JsonOptions);
        source["language"] ??= LanguageCodes.English;
        source["calibration"] ??= JsonSerializer.SerializeToNode(
            new CalibrationConfig(),
            JsonOptions);
    }

    private static JsonObject BuildLegacyDarkPalette(JsonObject appearance)
    {
        var defaults = new BubblePalette();
        return new JsonObject
        {
            ["assistantBubble"] =
                appearance["assistantBubble"]?.DeepClone() ??
                JsonValue.Create(defaults.AssistantBubble),
            ["assistantText"] =
                appearance["assistantText"]?.DeepClone() ??
                JsonValue.Create(defaults.AssistantText),
            ["nicknameColor"] =
                appearance["nicknameColor"]?.DeepClone() ??
                JsonValue.Create(defaults.NicknameColor),
            ["avatarBackground"] = JsonValue.Create(defaults.AvatarBackground),
            ["avatarBorder"] = JsonValue.Create(defaults.AvatarBorder)
        };
    }

    private static bool EnsureValue(
        JsonObject target,
        string property,
        JsonNode? value)
    {
        if (target[property] is not null)
        {
            return false;
        }
        target[property] = value;
        return true;
    }
}

internal sealed record ConfigMigrationResult(AppConfig Config, bool WasMigrated);
