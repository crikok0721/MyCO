using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyCodex.Configuration;

public sealed class ConfigStore
{
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
            var json = await File.ReadAllTextAsync(_paths.ConfigFile, cancellationToken)
                .ConfigureAwait(false);
            var node = JsonNode.Parse(json)?.AsObject()
                       ?? throw new JsonException("Configuration root must be an object.");
            var schemaVersion = node["schemaVersion"]?.GetValue<int>() ?? 0;
            var migrated = schemaVersion != 1;
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
            Validate(config);
            if (migrated)
            {
                await SaveAsync(config, cancellationToken).ConfigureAwait(false);
            }
            return new ConfigLoadResult(config, false, migrated, null);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var backup = Path.Combine(
                _paths.BackupsDirectory,
                $"config.corrupt-{timestamp}.json");
            File.Copy(_paths.ConfigFile, backup, overwrite: false);
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
        Validate(config);
        var normalized = config with
        {
            Language = LanguageCodes.Normalize(config.Language),
            Assistant = config.Assistant with
            {
                Name = NicknameValidator.Normalize(config.Assistant.Name)
            },
            User = config.User with
            {
                Name = NicknameValidator.Normalize(config.User.Name)
            }
        };
        var temporary = _paths.ConfigFile + ".tmp";
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(temporary, json, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, _paths.ConfigFile, overwrite: true);

        var calibrationTemporary = _paths.CalibrationFile + ".tmp";
        var calibrationJson = JsonSerializer.Serialize(normalized.Calibration, JsonOptions);
        await File.WriteAllTextAsync(
            calibrationTemporary,
            calibrationJson,
            cancellationToken).ConfigureAwait(false);
        File.Move(calibrationTemporary, _paths.CalibrationFile, overwrite: true);
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
            var json = await File.ReadAllTextAsync(
                _paths.CalibrationFile,
                cancellationToken).ConfigureAwait(false);
            var calibration = JsonSerializer.Deserialize<CalibrationConfig>(json, JsonOptions)
                              ?? throw new JsonException(
                                  "Calibration configuration could not be deserialized.");
            if (calibration.SchemaVersion != 1)
            {
                throw new InvalidOperationException("Unsupported calibration schema.");
            }
            return calibration;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var backup = Path.Combine(
                _paths.BackupsDirectory,
                $"calibration.corrupt-{timestamp}.json");
            File.Copy(_paths.CalibrationFile, backup, overwrite: false);
            var temporary = _paths.CalibrationFile + ".tmp";
            var json = JsonSerializer.Serialize(fallback, JsonOptions);
            await File.WriteAllTextAsync(
                temporary,
                json,
                cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _paths.CalibrationFile, overwrite: true);
            return fallback;
        }
    }

    private static AppConfig Deserialize(JsonNode node)
    {
        return node.Deserialize<AppConfig>(JsonOptions)
               ?? throw new JsonException("Configuration could not be deserialized.");
    }

    private static void Validate(AppConfig config)
    {
        if (config.SchemaVersion != 1 ||
            config.ProtocolVersion != 1 ||
            config.Calibration.SchemaVersion != 1)
        {
            throw new InvalidOperationException("Unsupported configuration schema.");
        }
        NicknameValidator.Normalize(config.Assistant.Name);
        NicknameValidator.Normalize(config.User.Name);
        LanguageCodes.Normalize(config.Language);
        if (config.Appearance.AvatarSize is < 24 or > 96 ||
            config.Appearance.BubbleRadius is < 0 or > 36 ||
            config.Appearance.BubblePaddingX is < 4 or > 40 ||
            config.Appearance.BubblePaddingY is < 4 or > 32 ||
            config.Appearance.MessageGap is < 4 or > 80 ||
            config.Appearance.MessageMaxWidth is < 35 or > 90)
        {
            throw new ArgumentException("Appearance values are outside supported ranges.");
        }
    }
}

internal static class ConfigMigration
{
    public static AppConfig Migrate(JsonObject source)
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
