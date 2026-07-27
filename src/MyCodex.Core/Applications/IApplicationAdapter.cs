// Maps a discovered desktop application to the arguments needed to expose local CDP.
namespace MyCodex.Applications;

public interface IApplicationAdapter
{
    string Id { get; }
    int Score(ApplicationCandidate candidate);
    IReadOnlyList<string> BuildLaunchArguments(int cdpPort);
    IReadOnlyList<string> BuildPipeLaunchArguments() =>
    [
        "--remote-debugging-pipe"
    ];
}

public sealed class ChatGptDesktopAdapter : IApplicationAdapter
{
    public string Id => "chatgpt-desktop";

    public int Score(ApplicationCandidate candidate)
    {
        var score = 0;
        score += candidate.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)
            ? 50
            : 0;
        score += candidate.PackageIdentity?.StartsWith(
            "OpenAI.Codex_",
            StringComparison.OrdinalIgnoreCase) == true
            ? 40
            : 0;
        score += candidate.ExecutablePath?.Contains(
            "WindowsApps",
            StringComparison.OrdinalIgnoreCase) == true
            ? 10
            : 0;
        return score;
    }

    public IReadOnlyList<string> BuildLaunchArguments(int cdpPort) =>
    [
        "--remote-debugging-address=127.0.0.1",
        $"--remote-debugging-port={cdpPort}"
    ];
}

public sealed class LegacyCodexAdapter : IApplicationAdapter
{
    public string Id => "legacy-codex";

    public int Score(ApplicationCandidate candidate)
    {
        if (!candidate.ProcessName.Equals("Codex", StringComparison.OrdinalIgnoreCase) ||
            candidate.ExecutablePath?.Contains(
                $"{Path.DirectorySeparatorChar}resources{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return 0;
        }
        return 75;
    }

    public IReadOnlyList<string> BuildLaunchArguments(int cdpPort) =>
    [
        "--remote-debugging-address=127.0.0.1",
        $"--remote-debugging-port={cdpPort}"
    ];
}

public sealed class ApplicationAdapterCatalog
{
    private readonly IReadOnlyList<IApplicationAdapter> _adapters =
    [
        new ChatGptDesktopAdapter(),
        new LegacyCodexAdapter()
    ];

    public IApplicationAdapter? Select(ApplicationCandidate candidate)
    {
        // Adapters score the same candidate independently; the highest positive score wins.
        return _adapters
            .Select(adapter => (Adapter: adapter, Score: adapter.Score(candidate)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .Select(result => result.Adapter)
            .FirstOrDefault();
    }
}
