using System.Globalization;

namespace MyCO.Manager.Services;

// The uptime-derived boot marker is stable across MyCO restarts during one
// Windows session and changes after the operating system boots again.
internal static class SystemBootIdentity
{
    public static string Current()
    {
        var estimatedBoot = DateTimeOffset.UtcNow -
                             TimeSpan.FromMilliseconds(Environment.TickCount64);
        var ticks = estimatedBoot.UtcTicks -
                    estimatedBoot.UtcTicks % TimeSpan.TicksPerMinute;
        return new DateTimeOffset(ticks, TimeSpan.Zero)
            .ToString("O", CultureInfo.InvariantCulture);
    }
}

internal static class TrayNotificationPolicy
{
    public static bool ShouldNotify(
        bool userInitiated,
        string currentBootId,
        string? lastNotifiedBootId)
    {
        return userInitiated &&
               !string.IsNullOrWhiteSpace(currentBootId) &&
               !string.Equals(
                   currentBootId,
                   lastNotifiedBootId,
                   StringComparison.Ordinal);
    }
}
