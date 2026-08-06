namespace MyCO.Manager.Services;

internal static class TrayNotificationPolicy
{
    public static bool ShouldNotify(
        bool userInitiated,
        bool alreadyPresented)
    {
        return userInitiated && !alreadyPresented;
    }

    public static bool TryPresent(
        bool userInitiated,
        bool alreadyPresented,
        Action present,
        out bool nextAlreadyPresented)
    {
        ArgumentNullException.ThrowIfNull(present);
        nextAlreadyPresented = alreadyPresented;
        if (!ShouldNotify(userInitiated, alreadyPresented))
        {
            return false;
        }

        try
        {
            present();
        }
        catch (Exception)
        {
            // A failed shell request must not consume the current-process claim.
            return false;
        }

        nextAlreadyPresented = true;
        return true;
    }
}
