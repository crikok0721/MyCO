using System.Text.Json;
using System.Text.Json.Serialization;
using MyCO.Avatars;
using MyCO.Configuration;

// Produces the small JSON object consumed by the in-page TypeScript runtime.
namespace MyCO.Injection;

public static class RuntimeConfigSerializer
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    public static async Task<string> SerializeAsync(
        AppConfig config,
        string bindingName,
        CancellationToken cancellationToken = default)
    {
        var paths = new ConfigPaths();
        var avatarService = new AvatarService(paths.AvatarsDirectory);
        return await SerializeAsync(
            config,
            bindingName,
            avatarService,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<string> SerializeAsync(
        AppConfig config,
        string bindingName,
        AvatarService avatarService,
        CancellationToken cancellationToken = default)
    {
        // Renderer pages cannot read the local avatar files, so inline them as data URLs.
        var assistantAvatar = await avatarService.ToDataUrlAsync(
            config.Assistant.Avatar,
            cancellationToken).ConfigureAwait(false);
        var userAvatar = await avatarService.ToDataUrlAsync(
            config.User.Avatar,
            cancellationToken).ConfigureAwait(false);
        var geometry = config.Appearance.Geometry.IsZero
            ? AppearanceGeometryResolver.FromAbsolute(config.Appearance)
            : config.Appearance.Geometry;
        var effective = AppearanceGeometryResolver.Resolve(geometry);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = BuildInfo.ConfigSchemaVersion,
            protocolVersion = BuildInfo.ProtocolVersion,
            language = LanguageCodes.Normalize(config.Language),
            assistant = new
            {
                name = config.Assistant.Name,
                avatar = assistantAvatar
            },
            user = new
            {
                name = config.User.Name,
                avatar = userAvatar
            },
            appearance = new
            {
                preset = config.Appearance.Preset,
                bubbleDisplayMode = config.Appearance.BubbleDisplayMode,
                avatarSize = effective.AvatarSize,
                assistantAvatarOffsetX = effective.AssistantAvatarOffsetX,
                assistantAvatarOffsetY = effective.AssistantAvatarOffsetY,
                userAvatarOffsetX = effective.UserAvatarOffsetX,
                userAvatarOffsetY = effective.UserAvatarOffsetY,
                assistantNicknameOffsetX = effective.AssistantNicknameOffsetX,
                assistantNicknameOffsetY = effective.AssistantNicknameOffsetY,
                userNicknameOffsetX = effective.UserNicknameOffsetX,
                userNicknameOffsetY = effective.UserNicknameOffsetY,
                bubbleRadius = effective.BubbleRadius,
                bubblePaddingX = effective.BubblePaddingX,
                bubblePaddingY = effective.BubblePaddingY,
                nicknameVisible = config.Appearance.NicknameVisible,
                messageGap = effective.MessageGap,
                assistantBubbleMaxWidth = effective.AssistantBubbleMaxWidth,
                geometryBaselineVersion = AppearanceGeometryResolver.BaselineVersion,
                geometry,
                darkBubblePalette = config.Appearance.DarkBubblePalette,
                lightBubblePalette = config.Appearance.LightBubblePalette
            },
            calibration = config.Calibration,
            bridgeBindingName = bindingName
        }, JsonOptions);
    }
}
