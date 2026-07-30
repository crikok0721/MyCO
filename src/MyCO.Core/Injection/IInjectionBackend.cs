using MyCO.Cdp;
using MyCO.Configuration;

// Separates session orchestration from the concrete CDP injection mechanism.
namespace MyCO.Injection;

public interface IInjectionBackend
{
    string Id { get; }

    Task<RuntimeInjectionResult> InjectAsync(
        CdpTarget target,
        ICdpClient client,
        string runtimeScript,
        AppConfig config,
        CancellationToken cancellationToken = default);
}

public sealed class CdpInjectionBackend : IInjectionBackend
{
    private readonly RuntimeInjector _injector;

    public CdpInjectionBackend(RuntimeInjector? injector = null)
    {
        _injector = injector ?? new RuntimeInjector();
    }

    public string Id => "cdp";

    public Task<RuntimeInjectionResult> InjectAsync(
        CdpTarget target,
        ICdpClient client,
        string runtimeScript,
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        return _injector.InjectAsync(
            target,
            client,
            runtimeScript,
            config,
            cancellationToken);
    }
}
