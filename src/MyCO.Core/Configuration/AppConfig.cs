using MyCO.Compatibility;

// Defines the versioned settings shared by the WPF manager and browser runtime.
namespace MyCO.Configuration;

public sealed record PersonConfig
{
    public string Name { get; init; } = string.Empty;
    public string Avatar { get; init; } = string.Empty;
}

public enum ManagerThemeMode
{
    System,
    Dark,
    Light
}

public enum BubbleDisplayMode
{
    Automatic,
    Whole
}

public sealed record BubblePalette
{
    public string AssistantBubble { get; init; } = "#222222";
    public string AssistantText { get; init; } = "#F2F2F2";
    public string NicknameColor { get; init; } = "#9A9A9A";
    public string AvatarBackground { get; init; } = "#303030";
    public string AvatarBorder { get; init; } = "#FFFFFF14";

    public static BubblePalette LightDefault => new()
    {
        AssistantBubble = "#F1F3F5",
        AssistantText = "#202124",
        NicknameColor = "#5F6672",
        AvatarBackground = "#E5E7EB",
        AvatarBorder = "#00000024"
    };
}

public sealed record AppearanceConfig
{
    public string Preset { get; init; } = "ReferenceDark";
    public BubbleDisplayMode BubbleDisplayMode { get; init; } =
        BubbleDisplayMode.Automatic;
    public int AvatarSize { get; init; } = 40;
    public int AvatarOffsetX { get; init; }
    public int AvatarOffsetY { get; init; } = 11;
    public int BubbleRadius { get; init; } = 14;
    public int BubblePaddingX { get; init; } = 14;
    public int BubblePaddingY { get; init; } = 10;
    public bool NicknameVisible { get; init; } = true;
    public int MessageGap { get; init; } = 28;
    public int MessageMaxWidth { get; init; } = 66;
    // Retained only so schema migration never discards legacy user colors.
    // Runtime deliberately ignores both fields and leaves the native User bubble untouched.
    public string UserBubble { get; init; } = "#242424";
    public string UserText { get; init; } = "#F5F5F5";
    public BubblePalette DarkBubblePalette { get; init; } = new();
    public BubblePalette LightBubblePalette { get; init; } =
        BubblePalette.LightDefault;
}

public sealed record CalibrationConfig
{
    public int SchemaVersion { get; init; } = BuildInfo.CalibrationSchemaVersion;
    public ElementSignature? UserTurn { get; init; }
    public ElementSignature? AssistantTurn { get; init; }
}

public sealed record AppConfig
{
    public int SchemaVersion { get; init; } = BuildInfo.ConfigSchemaVersion;
    public int ProtocolVersion { get; init; } = BuildInfo.ProtocolVersion;
    public string Language { get; init; } = LanguageCodes.English;
    public ManagerThemeMode ManagerThemeMode { get; init; } =
        ManagerThemeMode.System;
    public bool LaunchAtLogin { get; init; }
    public bool LaunchCodexOnMycoStart { get; init; }
    public PersonConfig Assistant { get; init; } = new() { Name = "菲叶子" };
    public PersonConfig User { get; init; } = new() { Name = "You" };
    public AppearanceConfig Appearance { get; init; } = new();
    public CalibrationConfig Calibration { get; init; } = new();

    public static AppConfig Default => new();
}

public sealed record ConfigLoadResult(
    AppConfig Config,
    bool WasCreated,
    bool WasMigrated,
    string? CorruptBackupPath);
