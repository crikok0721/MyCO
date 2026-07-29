using System.Text.Json;
using System.Text.Json.Nodes;
using MyCodex.Avatars;
using MyCodex.Configuration;
using MyCodex.Injection;

// Verifies config creation, migration, validation, recovery, and atomic persistence.
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
              "userName": "Avery",
              "userAvatar": "user.png"
            }
            """);

        var result = await new ConfigStore(paths).LoadAsync();

        Assert.True(result.WasMigrated);
        Assert.Equal(3, result.Config.SchemaVersion);
        Assert.Equal(
            BubbleDisplayMode.Automatic,
            result.Config.Appearance.BubbleDisplayMode);
        Assert.Equal("Luna", result.Config.Assistant.Name);
        Assert.Equal("Avery", result.Config.User.Name);
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
            Appearance = AppConfig.Default.Appearance with
            {
                AvatarSize = 52,
                AvatarOffsetX = 7,
                AvatarOffsetY = 13
            }
        };

        await store.SaveAsync(expected);
        var loaded = await store.LoadAsync();

        Assert.Equal(LanguageCodes.TraditionalChinese, loaded.Config.Language);
        Assert.Equal("Luna", loaded.Config.Assistant.Name);
        Assert.Equal(52, loaded.Config.Appearance.AvatarSize);
        Assert.Equal(7, loaded.Config.Appearance.AvatarOffsetX);
        Assert.Equal(13, loaded.Config.Appearance.AvatarOffsetY);
        Assert.Equal(1, loaded.Config.Calibration.SchemaVersion);
    }

    [Fact]
    public async Task SchemaTwoAddsAutomaticBubbleModeWithoutLosingSettings()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        var store = new ConfigStore(paths);
        await store.SaveAsync(
            AppConfig.Default with
            {
                Appearance = AppConfig.Default.Appearance with
                {
                    AvatarSize = 55
                }
            });
        var root = JsonNode.Parse(await File.ReadAllTextAsync(paths.ConfigFile))!
            .AsObject();
        root["schemaVersion"] = 2;
        root["appearance"]!.AsObject().Remove("bubbleDisplayMode");
        await File.WriteAllTextAsync(paths.ConfigFile, root.ToJsonString());

        var loaded = await store.LoadAsync();

        Assert.True(loaded.WasMigrated);
        Assert.Equal(55, loaded.Config.Appearance.AvatarSize);
        Assert.Equal(
            BubbleDisplayMode.Automatic,
            loaded.Config.Appearance.BubbleDisplayMode);
        Assert.Equal(3, loaded.Config.SchemaVersion);
    }

    [Fact]
    public async Task WholeBubbleModePersistsAcrossManagerRestart()
    {
        using var directory = new TempDirectory();
        var store = new ConfigStore(new ConfigPaths(directory.Path));
        await store.SaveAsync(
            AppConfig.Default with
            {
                Appearance = AppConfig.Default.Appearance with
                {
                    BubbleDisplayMode = BubbleDisplayMode.Whole
                }
            });

        var loaded = await store.LoadAsync();

        Assert.Equal(
            BubbleDisplayMode.Whole,
            loaded.Config.Appearance.BubbleDisplayMode);
    }

    [Fact]
    public async Task ExistingSchemaDefaultsMissingAvatarOffsetsSafely()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(
            paths.ConfigFile,
            """
            {
              "schemaVersion": 1,
              "protocolVersion": 1,
              "language": "zh-CN",
              "assistant": { "name": "Codex", "avatar": "" },
              "user": { "name": "You", "avatar": "" },
              "appearance": {
                "preset": "ReferenceDark",
                "avatarSize": 40,
                "bubbleRadius": 14,
                "bubblePaddingX": 14,
                "bubblePaddingY": 10,
                "nicknameVisible": true,
                "messageGap": 28,
                "messageMaxWidth": 66,
                "userBubble": "#242424",
                "assistantBubble": "#222222",
                "userText": "#f5f5f5",
                "assistantText": "#f2f2f2",
                "nicknameColor": "#9a9a9a"
              },
              "calibration": { "schemaVersion": 1 }
            }
            """);

        var loaded = await new ConfigStore(paths).LoadAsync();

        Assert.Equal(0, loaded.Config.Appearance.AvatarOffsetX);
        Assert.Equal(11, loaded.Config.Appearance.AvatarOffsetY);
    }

    [Fact]
    public async Task SchemaOnePreservesAppearanceAndInvalidatesSingleSampleCalibration()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(
            paths.ConfigFile,
            """
            {
              "schemaVersion": 1,
              "protocolVersion": 1,
              "language": "zh-TW",
              "assistant": { "name": "露娜", "avatar": "C:\\头像\\assistant.png" },
              "user": { "name": "Avery", "avatar": "C:\\头像\\user.png" },
              "appearance": {
                "preset": "ReferenceDark",
                "avatarSize": 52,
                "avatarOffsetX": 7,
                "avatarOffsetY": 13,
                "bubbleRadius": 19,
                "bubblePaddingX": 17,
                "bubblePaddingY": 12,
                "nicknameVisible": false,
                "messageGap": 33,
                "messageMaxWidth": 72,
                "userBubble": "#242424",
                "assistantBubble": "#123456",
                "userText": "#F5F5F5",
                "assistantText": "#FFFFFF",
                "nicknameColor": "#ABCDEF"
              },
              "calibration": {
                "schemaVersion": 1,
                "assistantTurn": {
                  "schemaVersion": 1,
                  "tagName": "article",
                  "role": "article",
                  "stableAttributes": { "data-role": "assistant" },
                  "stableClasses": [],
                  "ancestorChain": [],
                  "childTagHistogram": { "p": 1 },
                  "capabilities": {
                    "hasMarkdown": true,
                    "hasCode": false,
                    "hasButtons": false
                  },
                  "layout": { "alignment": "left", "widthRatio": 0.7 },
                  "fingerprint": "article;assistant"
                }
              }
            }
            """);

        var loaded = await new ConfigStore(paths).LoadAsync();

        Assert.True(loaded.WasMigrated);
        Assert.Equal(3, loaded.Config.SchemaVersion);
        Assert.Equal("露娜", loaded.Config.Assistant.Name);
        Assert.Equal(@"C:\头像\assistant.png", loaded.Config.Assistant.Avatar);
        Assert.Equal(52, loaded.Config.Appearance.AvatarSize);
        Assert.Equal(7, loaded.Config.Appearance.AvatarOffsetX);
        Assert.Equal(13, loaded.Config.Appearance.AvatarOffsetY);
        Assert.Equal(19, loaded.Config.Appearance.BubbleRadius);
        Assert.False(loaded.Config.Appearance.NicknameVisible);
        Assert.Equal(
            "#123456",
            loaded.Config.Appearance.DarkBubblePalette.AssistantBubble);
        Assert.Equal(
            "#FFFFFF",
            loaded.Config.Appearance.DarkBubblePalette.AssistantText);
        Assert.Equal(
            "#ABCDEF",
            loaded.Config.Appearance.DarkBubblePalette.NicknameColor);
        Assert.Equal("#242424", loaded.Config.Appearance.UserBubble);
        Assert.Equal("#F5F5F5", loaded.Config.Appearance.UserText);
        Assert.Equal(
            BubblePalette.LightDefault,
            loaded.Config.Appearance.LightBubblePalette);
        Assert.Null(loaded.Config.Calibration.AssistantTurn);
        Assert.Equal(ManagerThemeMode.System, loaded.Config.ManagerThemeMode);
        Assert.False(loaded.Config.LaunchAtLogin);
        Assert.False(loaded.Config.LaunchCodexOnMyCodexStart);
    }

    [Theory]
    [InlineData(ManagerThemeMode.Dark)]
    [InlineData(ManagerThemeMode.Light)]
    [InlineData(ManagerThemeMode.System)]
    public async Task ManagerThemeAndStartupOptionsRoundTrip(
        ManagerThemeMode theme)
    {
        using var directory = new TempDirectory();
        var store = new ConfigStore(new ConfigPaths(directory.Path));
        await store.SaveAsync(
            AppConfig.Default with
            {
                ManagerThemeMode = theme,
                LaunchAtLogin = true,
                LaunchCodexOnMyCodexStart = true
            });

        var loaded = await store.LoadAsync();

        Assert.Equal(theme, loaded.Config.ManagerThemeMode);
        Assert.True(loaded.Config.LaunchAtLogin);
        Assert.True(loaded.Config.LaunchCodexOnMyCodexStart);
    }

    [Fact]
    public async Task CurrentSchemaWithMissingNewFieldsIsRepairedWithoutLosingIdentity()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(
            paths.ConfigFile,
            """
            {
              "schemaVersion": 2,
              "protocolVersion": 1,
              "language": "zh-CN",
              "assistant": { "name": "小助手", "avatar": "C:\\资料 空格\\a.png" },
              "user": { "name": "用户", "avatar": "" },
              "appearance": {
                "preset": "ReferenceDark",
                "avatarSize": 40,
                "avatarOffsetX": 0,
                "avatarOffsetY": 11,
                "bubbleRadius": 14,
                "bubblePaddingX": 14,
                "bubblePaddingY": 10,
                "nicknameVisible": true,
                "messageGap": 28,
                "messageMaxWidth": 66
              },
              "calibration": { "schemaVersion": 1 }
            }
            """);

        var loaded = await new ConfigStore(paths).LoadAsync();

        Assert.True(loaded.WasMigrated);
        Assert.Equal("小助手", loaded.Config.Assistant.Name);
        Assert.Equal(@"C:\资料 空格\a.png", loaded.Config.Assistant.Avatar);
        Assert.Equal(new BubblePalette(), loaded.Config.Appearance.DarkBubblePalette);
        Assert.Equal(
            BubblePalette.LightDefault,
            loaded.Config.Appearance.LightBubblePalette);
        Assert.Equal(ManagerThemeMode.System, loaded.Config.ManagerThemeMode);
    }

    [Fact]
    public async Task RuntimeSerializationCarriesBothPalettesAndBubbleMode()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        var config = AppConfig.Default with
        {
            Appearance = AppConfig.Default.Appearance with
            {
                DarkBubblePalette = new BubblePalette
                {
                    AssistantBubble = "#101214",
                    AssistantText = "#F4F5F6"
                },
                LightBubblePalette = BubblePalette.LightDefault with
                {
                    AssistantBubble = "#FAFBFC",
                    AssistantText = "#202124"
                },
                BubbleDisplayMode = BubbleDisplayMode.Whole
            }
        };

        var json = await RuntimeConfigSerializer.SerializeAsync(
            config,
            "__mc_test",
            new AvatarService(paths.AvatarsDirectory));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "Whole",
            root.GetProperty("appearance")
                .GetProperty("bubbleDisplayMode")
                .GetString());
        Assert.Equal(
            "#101214",
            root.GetProperty("appearance")
                .GetProperty("darkBubblePalette")
                .GetProperty("assistantBubble")
                .GetString());
        Assert.Equal(
            "#FAFBFC",
            root.GetProperty("appearance")
                .GetProperty("lightBubblePalette")
                .GetProperty("assistantBubble")
                .GetString());
        Assert.Equal("__mc_test", root.GetProperty("bridgeBindingName").GetString());
    }

    [Fact]
    public async Task UnreadableBubblePaletteContrastIsRejected()
    {
        using var directory = new TempDirectory();
        var store = new ConfigStore(new ConfigPaths(directory.Path));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                AppConfig.Default with
                {
                    Appearance = AppConfig.Default.Appearance with
                    {
                        LightBubblePalette =
                            BubblePalette.LightDefault with
                            {
                                AssistantBubble = "#FFFFFF",
                                AssistantText = "#FFFFFF"
                            }
                    }
                }));
    }

    [Fact]
    public async Task AvatarOffsetsOutsideSliderRangeAreRejected()
    {
        using var directory = new TempDirectory();
        var store = new ConfigStore(new ConfigPaths(directory.Path));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                AppConfig.Default with
                {
                    Appearance = AppConfig.Default.Appearance with
                    {
                        AvatarOffsetX = 33
                    }
                }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                AppConfig.Default with
                {
                    Appearance = AppConfig.Default.Appearance with
                    {
                        AvatarOffsetY = 41
                    }
                }));
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
        Assert.Equal(3, document.RootElement.GetProperty("schemaVersion").GetInt32());
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

    [Fact]
    public async Task AmbiguousCalibrationIsBackedUpAndQuarantined()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        var store = new ConfigStore(paths);
        await store.SaveAsync(AppConfig.Default);
        var signature = new MyCodex.Compatibility.ElementSignature
        {
            SampleCount = 3,
            ContextFingerprint = "main;;thread",
            TagName = "div",
            StableAttributes =
            {
                ["data-content-search-unit-key"] = "present"
            },
            Capabilities = new MyCodex.Compatibility.SignatureCapabilities
            {
                HasMarkdown = true
            }
        };
        await File.WriteAllTextAsync(
            paths.CalibrationFile,
            JsonSerializer.Serialize(
                new CalibrationConfig
                {
                    UserTurn = signature,
                    AssistantTurn = signature
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var loaded = await store.LoadAsync();

        Assert.Null(loaded.Config.Calibration.UserTurn);
        Assert.Null(loaded.Config.Calibration.AssistantTurn);
        Assert.Single(
            Directory.GetFiles(paths.BackupsDirectory, "calibration.corrupt-*.json"));
    }

    [Fact]
    public async Task LegacySingleSampleCalibrationIsInvalidatedWithoutLosingPreferences()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        var store = new ConfigStore(paths);
        var legacy = new MyCodex.Compatibility.ElementSignature
        {
            TagName = "article",
            StableAttributes =
            {
                ["data-message-author-role"] = "assistant"
            }
        };
        await store.SaveAsync(
            AppConfig.Default with
            {
                Assistant = new PersonConfig
                {
                    Name = "Luna",
                    Avatar = "avatar.png"
                }
            });
        await File.WriteAllTextAsync(
            paths.CalibrationFile,
            JsonSerializer.Serialize(
                new CalibrationConfig { AssistantTurn = legacy },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var loaded = await store.LoadAsync();

        Assert.Equal("Luna", loaded.Config.Assistant.Name);
        Assert.Equal("avatar.png", loaded.Config.Assistant.Avatar);
        Assert.Null(loaded.Config.Calibration.AssistantTurn);
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
