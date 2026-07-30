// Abstraction used by the manager and tests to discover supported desktop installations.
namespace MyCO.Applications;

public interface IApplicationLocator
{
    Task<IReadOnlyList<ApplicationCandidate>> FindCandidatesAsync(
        CancellationToken cancellationToken = default);
}
