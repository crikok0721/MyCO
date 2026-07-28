using System.Text.Json;
using MyCodex.Avatars;
using MyCodex.Configuration;

// Produces the small JSON object consumed by the in-page TypeScript runtime.
namespace MyCodex.Injection;

public static class RuntimeConfigSerializer
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

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
        return JsonSerializer.Serialize(new
        {
            schemaVersion = BuildInfo.ConfigSchemaVersion,
            protocolVersion = BuildInfo.ProtocolVersion,
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
            appearance = config.Appearance,
            calibration = config.Calibration,
            bridgeBindingName = bindingName
        }, JsonOptions);
    }
}
