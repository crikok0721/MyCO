namespace MyCO.Startup;

public sealed record StartupRegistrationStatus(
    bool IsRegistered,
    bool MatchesCurrentExecutable,
    string? RegisteredCommand);

public interface IStartupRegistrationService
{
    StartupRegistrationStatus GetStatus(string executablePath);

    void SetEnabled(string executablePath, bool enabled);

    void Restore(StartupRegistrationStatus status);
}
