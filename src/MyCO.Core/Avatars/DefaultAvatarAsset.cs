using MyCO.Configuration;

// Identifies the repository-owned assistant avatar and seeds it into the
// same managed storage used by user-selected avatars. The pack URI is only a
// host-side resource locator; it is never persisted in user configuration.
namespace MyCO.Avatars;

public static class DefaultAvatarAsset
{
    public const string FileName = "MyCO-logo.png";
    public const string ResourceUri =
        "pack://application:,,,/Assets/MyCO-logo.png";

    public static async Task<AppConfig> SeedAsync(
        AppConfig config,
        AvatarService avatarService,
        Stream resource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(avatarService);
        ArgumentNullException.ThrowIfNull(resource);
        if (!string.IsNullOrWhiteSpace(config.Assistant.Avatar))
        {
            return config;
        }

        var imported = await avatarService.ImportAsync(
                resource,
                cancellationToken)
            .ConfigureAwait(false);
        return config with
        {
            Assistant = config.Assistant with
            {
                Avatar = imported.StoredPath
            }
        };
    }
}
