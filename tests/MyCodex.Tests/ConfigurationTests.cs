using System.Text.Json;
using MyCodex.Configuration;

namespace MyCodex.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public async Task MissingConfigCreatesDefaultsAndLoadsAgain()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        var store = new ConfigStore(paths);

        var first = await store.LoadAsync();
        var second = await store.LoadAsync();

        Assert.True(first.WasCreated);
        Assert.False(second.WasCreated);
        Assert.Equal("Codex", second.Config.Assistant.Name);
        Assert.Equal("You", second.Config.User.Name);
        Assert.Equal(LanguageCodes.English, second.Config.Language);
        Assert.True(File.Exists(paths.ConfigFile));
        Assert.True(File.Exists(paths.CalibrationFile));
    }

    [Fact]
    public async Task SchemaZeroMigratesNamesWithoutLosingValues()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(
            paths.ConfigFile,
            """
            {
              "schemaVersion": 0,
              "assistantName": "Luna",
              "assistantAvatar": "luna.png",
              "userName": "crikok",
              "userAvatar": "user.png"
            }
            """);

        var result = await new ConfigStore(paths).LoadAsync();

        Assert.True(result.WasMigrated);
        Assert.Equal(1, result.Config.SchemaVersion);
        Assert.Equal("Luna", result.Config.Assistant.Name);
        Assert.Equal("crikok", result.Config.User.Name);
        Assert.Equal("luna.png", result.Config.Assistant.Avatar);
    }

    [Fact]
    public async Task LanguageRoundTripsWithoutChangingAppearanceOrCalibration()
    {
        using var directory = new TempDirectory();
        var store = new ConfigStore(new ConfigPaths(directory.Path));
        var expected = AppConfig.Default with
        {
            Language = LanguageCodes.TraditionalChinese,
            Assistant = new PersonConfig { Name = "Luna" },
            Appearance = AppConfig.Default.Appearance with { AvatarSize = 52 }
        };

        await store.SaveAsync(expected);
        var loaded = await store.LoadAsync();

        Assert.Equal(LanguageCodes.TraditionalChinese, loaded.Config.Language);
        Assert.Equal("Luna", loaded.Config.Assistant.Name);
        Assert.Equal(52, loaded.Config.Appearance.AvatarSize);
        Assert.Equal(1, loaded.Config.Calibration.SchemaVersion);
    }

    [Fact]
    public async Task CorruptJsonIsBackedUpAndDefaultsAreRestored()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ConfigFile, "{not-json");

        var result = await new ConfigStore(paths).LoadAsync();

        Assert.NotNull(result.CorruptBackupPath);
        Assert.True(File.Exists(result.CorruptBackupPath));
        Assert.Equal("Codex", result.Config.Assistant.Name);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(paths.ConfigFile));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task CorruptCalibrationIsBackedUpWithoutDiscardingMainConfig()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        var store = new ConfigStore(paths);
        await store.SaveAsync(
            AppConfig.Default with
            {
                Assistant = new PersonConfig { Name = "Luna" }
            });
        await File.WriteAllTextAsync(paths.CalibrationFile, "{not-json");

        var result = await store.LoadAsync();

        Assert.Equal("Luna", result.Config.Assistant.Name);
        Assert.Single(
            Directory.GetFiles(paths.BackupsDirectory, "calibration.corrupt-*.json"));
        using var repaired = JsonDocument.Parse(
            await File.ReadAllTextAsync(paths.CalibrationFile));
        Assert.Equal(
            1,
            repaired.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Theory]
    [InlineData(" Luna ", "Luna")]
    [InlineData("王10", "王10")]
    public void NicknameValidationNormalizesValidValues(string input, string expected)
    {
        Assert.Equal(expected, NicknameValidator.Normalize(input));
    }

    [Fact]
    public void NicknameValidationRejectsEmptyControlAndOversizedValues()
    {
        Assert.Throws<ArgumentException>(() => NicknameValidator.Normalize("  "));
        Assert.Throws<ArgumentException>(() => NicknameValidator.Normalize("bad\nname"));
        Assert.Throws<ArgumentException>(() =>
            NicknameValidator.Normalize(new string('x', 33)));
    }

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("ZH-cn", "zh-CN")]
    [InlineData("zh-TW", "zh-TW")]
    public void LanguageCodesNormalizeSupportedValues(string input, string expected)
    {
        Assert.Equal(expected, LanguageCodes.Normalize(input));
    }

    [Fact]
    public void LanguageCodesRejectUnsupportedValues()
    {
        Assert.Throws<ArgumentException>(() => LanguageCodes.Normalize("fr-FR"));
    }

    [Fact]
    public async Task UnsupportedStoredLanguageFallsBackWithoutLosingSettings()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        var store = new ConfigStore(paths);
        await store.SaveAsync(
            AppConfig.Default with
            {
                Assistant = new PersonConfig { Name = "Luna" }
            });
        var json = await File.ReadAllTextAsync(paths.ConfigFile);
        await File.WriteAllTextAsync(
            paths.ConfigFile,
            json.Replace("\"language\": \"en-US\"", "\"language\": \"fr-FR\""));

        var loaded = await store.LoadAsync();

        Assert.True(loaded.WasMigrated);
        Assert.Equal(LanguageCodes.English, loaded.Config.Language);
        Assert.Equal("Luna", loaded.Config.Assistant.Name);
        Assert.Null(loaded.CorruptBackupPath);
    }
}
