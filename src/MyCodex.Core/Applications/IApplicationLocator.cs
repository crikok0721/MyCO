namespace MyCodex.Applications;

public interface IApplicationLocator
{
    Task<IReadOnlyList<ApplicationCandidate>> FindCandidatesAsync(
        CancellationToken cancellationToken = default);
}
