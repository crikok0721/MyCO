using MyCodex.Compatibility;

// Defines the versioned settings shared by the WPF manager and browser runtime.
namespace MyCodex.Configuration;

public sealed record PersonConfig
{
    public string Name { get; init; } = string.Empty;
    public string Avatar { get; init; } = string.Empty;
}

public sealed record AppearanceConfig
{
    public string Preset { get; init; } = "ReferenceDark";
    public int AvatarSize { get; init; } = 40;
    public int AvatarOffsetX { get; init; }
    public int AvatarOffsetY { get; init; } = 11;
    public int BubbleRadius { get; init; } = 14;
    public int BubblePaddingX { get; init; } = 14;
    public int BubblePaddingY { get; init; } = 10;
    public bool NicknameVisible { get; init; } = true;
    public int MessageGap { get; init; } = 28;
    public int MessageMaxWidth { get; init; } = 66;
    public string UserBubble { get; init; } = "#242424";
    public string AssistantBubble { get; init; } = "#222222";
    public string UserText { get; init; } = "#f5f5f5";
    public string AssistantText { get; init; } = "#f2f2f2";
    public string NicknameColor { get; init; } = "#9a9a9a";
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
    public PersonConfig Assistant { get; init; } = new() { Name = "Codex" };
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
