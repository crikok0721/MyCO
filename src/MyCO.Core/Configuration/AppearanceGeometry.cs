using System.Text.Json.Serialization;

namespace MyCO.Configuration;

// Stable, privacy-safe geometry baseline used by both the Manager preview and
// the injected runtime. Persisted values are relative deltas; the effective
// values below are resolved for the current renderer surface before injection.
public sealed record AppearanceGeometryDeltas
{
    public int AvatarSizeDelta { get; init; }
    public int AssistantAvatarOffsetXDelta { get; init; }
    public int AssistantAvatarOffsetYDelta { get; init; }
    public int UserAvatarOffsetXDelta { get; init; }
    public int UserAvatarOffsetYDelta { get; init; }
    public int AssistantNicknameOffsetXDelta { get; init; }
    public int AssistantNicknameOffsetYDelta { get; init; }
    public int UserNicknameOffsetXDelta { get; init; }
    public int UserNicknameOffsetYDelta { get; init; }
    public int BubbleRadiusDelta { get; init; }
    public int BubblePaddingXDelta { get; init; }
    public int BubblePaddingYDelta { get; init; }
    public int MessageGapDelta { get; init; }
    public int AssistantBubbleMaxWidthDelta { get; init; }

    [JsonIgnore]
    public bool IsZero =>
        AvatarSizeDelta == 0 &&
        AssistantAvatarOffsetXDelta == 0 &&
        AssistantAvatarOffsetYDelta == 0 &&
        UserAvatarOffsetXDelta == 0 &&
        UserAvatarOffsetYDelta == 0 &&
        AssistantNicknameOffsetXDelta == 0 &&
        AssistantNicknameOffsetYDelta == 0 &&
        UserNicknameOffsetXDelta == 0 &&
        UserNicknameOffsetYDelta == 0 &&
        BubbleRadiusDelta == 0 &&
        BubblePaddingXDelta == 0 &&
        BubblePaddingYDelta == 0 &&
        MessageGapDelta == 0 &&
        AssistantBubbleMaxWidthDelta == 0;
}

public sealed record EffectiveAppearanceGeometry
{
    public int AvatarSize { get; init; }
    public int AssistantAvatarOffsetX { get; init; }
    public int AssistantAvatarOffsetY { get; init; }
    public int UserAvatarOffsetX { get; init; }
    public int UserAvatarOffsetY { get; init; }
    public int AssistantNicknameOffsetX { get; init; }
    public int AssistantNicknameOffsetY { get; init; }
    public int UserNicknameOffsetX { get; init; }
    public int UserNicknameOffsetY { get; init; }
    public int BubbleRadius { get; init; }
    public int BubblePaddingX { get; init; }
    public int BubblePaddingY { get; init; }
    public int MessageGap { get; init; }
    public int AssistantBubbleMaxWidth { get; init; }
}

public static class AppearanceGeometryResolver
{
    public const int LegacyBaselineVersion = 1;
    public const int BaselineVersion = 2;
    public const int AvatarSizeBaseline = 35;
    public const int AssistantAvatarOffsetYBaseline = 11;
    public const int UserAvatarOffsetYBaseline = -4;

    public static EffectiveAppearanceGeometry Resolve(
        AppearanceGeometryDeltas? deltas)
    {
        return ResolveWithBaselines(
            deltas,
            AvatarSizeBaseline,
            AssistantAvatarOffsetYBaseline,
            UserAvatarOffsetYBaseline);
    }

    // Schema-7 originally stored deltas against the v1 baseline. Resolve that
    // representation before converting it to the current baseline so an
    // upgrade preserves the user's effective geometry.
    public static EffectiveAppearanceGeometry ResolveLegacyBaselineOne(
        AppearanceGeometryDeltas? deltas)
    {
        return ResolveWithBaselines(deltas, 40, 11, 11);
    }

    public static AppearanceGeometryDeltas FromEffective(
        EffectiveAppearanceGeometry effective)
    {
        return new AppearanceGeometryDeltas
        {
            AvatarSizeDelta = effective.AvatarSize - AvatarSizeBaseline,
            AssistantAvatarOffsetXDelta = effective.AssistantAvatarOffsetX,
            AssistantAvatarOffsetYDelta =
                effective.AssistantAvatarOffsetY - AssistantAvatarOffsetYBaseline,
            UserAvatarOffsetXDelta = effective.UserAvatarOffsetX,
            UserAvatarOffsetYDelta =
                effective.UserAvatarOffsetY - UserAvatarOffsetYBaseline,
            AssistantNicknameOffsetXDelta = effective.AssistantNicknameOffsetX,
            AssistantNicknameOffsetYDelta = effective.AssistantNicknameOffsetY,
            UserNicknameOffsetXDelta = effective.UserNicknameOffsetX,
            UserNicknameOffsetYDelta = effective.UserNicknameOffsetY,
            BubbleRadiusDelta = effective.BubbleRadius - 14,
            BubblePaddingXDelta = effective.BubblePaddingX - 14,
            BubblePaddingYDelta = effective.BubblePaddingY - 10,
            MessageGapDelta = effective.MessageGap - 28,
            AssistantBubbleMaxWidthDelta = effective.AssistantBubbleMaxWidth - 66
        };
    }

    public static AppearanceGeometryDeltas ClampForMigration(
        AppearanceGeometryDeltas deltas)
    {
        return ClampDeltas(deltas);
    }

    private static EffectiveAppearanceGeometry ResolveWithBaselines(
        AppearanceGeometryDeltas? deltas,
        int avatarSizeBaseline,
        int assistantAvatarOffsetYBaseline,
        int userAvatarOffsetYBaseline)
    {
        deltas ??= new AppearanceGeometryDeltas();
        return new EffectiveAppearanceGeometry
        {
            AvatarSize = Math.Clamp(
                avatarSizeBaseline + deltas.AvatarSizeDelta,
                24,
                72),
            AssistantAvatarOffsetX = deltas.AssistantAvatarOffsetXDelta,
            AssistantAvatarOffsetY = Math.Clamp(
                assistantAvatarOffsetYBaseline + deltas.AssistantAvatarOffsetYDelta,
                -20,
                40),
            UserAvatarOffsetX = deltas.UserAvatarOffsetXDelta,
            UserAvatarOffsetY = Math.Clamp(
                userAvatarOffsetYBaseline + deltas.UserAvatarOffsetYDelta,
                -20,
                40),
            AssistantNicknameOffsetX = deltas.AssistantNicknameOffsetXDelta,
            AssistantNicknameOffsetY = Math.Clamp(deltas.AssistantNicknameOffsetYDelta, -12, 28),
            UserNicknameOffsetX = deltas.UserNicknameOffsetXDelta,
            UserNicknameOffsetY = Math.Clamp(deltas.UserNicknameOffsetYDelta, -12, 28),
            BubbleRadius = Math.Clamp(14 + deltas.BubbleRadiusDelta, 0, 36),
            BubblePaddingX = Math.Clamp(14 + deltas.BubblePaddingXDelta, 4, 40),
            BubblePaddingY = Math.Clamp(10 + deltas.BubblePaddingYDelta, 4, 32),
            MessageGap = Math.Clamp(28 + deltas.MessageGapDelta, 4, 80),
            AssistantBubbleMaxWidth = Math.Clamp(66 + deltas.AssistantBubbleMaxWidthDelta, 45, 80)
        };
    }

    public static AppearanceGeometryDeltas FromAbsolute(AppearanceConfig appearance)
    {
        return FromEffective(new EffectiveAppearanceGeometry
        {
            AvatarSize = appearance.AvatarSize,
            AssistantAvatarOffsetX = appearance.AssistantAvatarOffsetX,
            AssistantAvatarOffsetY = appearance.AssistantAvatarOffsetY,
            UserAvatarOffsetX = appearance.UserAvatarOffsetX,
            UserAvatarOffsetY = appearance.UserAvatarOffsetY,
            AssistantNicknameOffsetX = appearance.AssistantNicknameOffsetX,
            AssistantNicknameOffsetY = appearance.AssistantNicknameOffsetY,
            UserNicknameOffsetX = appearance.UserNicknameOffsetX,
            UserNicknameOffsetY = appearance.UserNicknameOffsetY,
            BubbleRadius = appearance.BubbleRadius,
            BubblePaddingX = appearance.BubblePaddingX,
            BubblePaddingY = appearance.BubblePaddingY,
            MessageGap = appearance.MessageGap,
            AssistantBubbleMaxWidth = appearance.AssistantBubbleMaxWidth
        });
    }

    private static AppearanceGeometryDeltas ClampDeltas(
        AppearanceGeometryDeltas deltas)
    {
        return deltas with
        {
            AvatarSizeDelta = Math.Clamp(deltas.AvatarSizeDelta, -32, 32),
            AssistantAvatarOffsetXDelta = Math.Clamp(
                deltas.AssistantAvatarOffsetXDelta,
                -32,
                32),
            AssistantAvatarOffsetYDelta = Math.Clamp(
                deltas.AssistantAvatarOffsetYDelta,
                -32,
                32),
            UserAvatarOffsetXDelta = Math.Clamp(
                deltas.UserAvatarOffsetXDelta,
                -32,
                32),
            UserAvatarOffsetYDelta = Math.Clamp(
                deltas.UserAvatarOffsetYDelta,
                -32,
                32),
            AssistantNicknameOffsetXDelta = Math.Clamp(
                deltas.AssistantNicknameOffsetXDelta,
                -32,
                32),
            AssistantNicknameOffsetYDelta = Math.Clamp(
                deltas.AssistantNicknameOffsetYDelta,
                -28,
                28),
            UserNicknameOffsetXDelta = Math.Clamp(
                deltas.UserNicknameOffsetXDelta,
                -32,
                32),
            UserNicknameOffsetYDelta = Math.Clamp(
                deltas.UserNicknameOffsetYDelta,
                -28,
                28),
            BubbleRadiusDelta = Math.Clamp(deltas.BubbleRadiusDelta, -18, 18),
            BubblePaddingXDelta = Math.Clamp(
                deltas.BubblePaddingXDelta,
                -18,
                18),
            BubblePaddingYDelta = Math.Clamp(
                deltas.BubblePaddingYDelta,
                -16,
                16),
            MessageGapDelta = Math.Clamp(deltas.MessageGapDelta, -40, 40),
            AssistantBubbleMaxWidthDelta = Math.Clamp(
                deltas.AssistantBubbleMaxWidthDelta,
                -20,
                20)
        };
    }

    public static void Validate(AppearanceGeometryDeltas deltas)
    {
        if (deltas.AvatarSizeDelta is < -32 or > 32 ||
            deltas.AssistantAvatarOffsetXDelta is < -32 or > 32 ||
            deltas.AssistantAvatarOffsetYDelta is < -32 or > 32 ||
            deltas.UserAvatarOffsetXDelta is < -32 or > 32 ||
            deltas.UserAvatarOffsetYDelta is < -32 or > 32 ||
            deltas.AssistantNicknameOffsetXDelta is < -32 or > 32 ||
            deltas.AssistantNicknameOffsetYDelta is < -28 or > 28 ||
            deltas.UserNicknameOffsetXDelta is < -32 or > 32 ||
            deltas.UserNicknameOffsetYDelta is < -28 or > 28 ||
            deltas.BubbleRadiusDelta is < -18 or > 18 ||
            deltas.BubblePaddingXDelta is < -18 or > 18 ||
            deltas.BubblePaddingYDelta is < -16 or > 16 ||
            deltas.MessageGapDelta is < -40 or > 40 ||
            deltas.AssistantBubbleMaxWidthDelta is < -20 or > 20)
        {
            throw new ArgumentException(
                "Appearance geometry deltas are outside supported ranges.");
        }
    }
}
