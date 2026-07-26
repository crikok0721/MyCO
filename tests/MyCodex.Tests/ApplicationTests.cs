using MyCodex.Applications;

namespace MyCodex.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void ChatGptAdapterScoresOfficialMsixAboveUnrelatedWin32()
    {
        var official = Candidate(
            "ChatGPT",
            @"C:\Program Files\WindowsApps\OpenAI.Codex_1.2.3.4_x64__publisher\app\ChatGPT.exe",
            "OpenAI.Codex_1.2.3.4_x64__publisher");
        var unrelated = Candidate("Other", @"C:\Tools\Other.exe", null);
        var adapter = new ChatGptDesktopAdapter();

        Assert.Equal(100, adapter.Score(official));
        Assert.Equal(0, adapter.Score(unrelated));
        Assert.Contains(
            adapter.BuildLaunchArguments(54321),
            argument => argument == "--remote-debugging-port=54321");
    }

    [Fact]
    public void LegacyAdapterRejectsPackagedCodexCliResource()
    {
        var cli = Candidate(
            "Codex",
            @"C:\Program Files\WindowsApps\OpenAI.Codex_x64\app\resources\codex.exe",
            "OpenAI.Codex_x64");
        Assert.Equal(0, new LegacyCodexAdapter().Score(cli));
    }

    [Fact]
    public void ResolverReplacesAStaleStoreVersionUsingStableLaunchIdentity()
    {
        var previous = Candidate(
            "ChatGPT",
            @"C:\Program Files\WindowsApps\OpenAI.Codex_26.721.3996.0_x64__publisher\app\ChatGPT.exe",
            "OpenAI.Codex_26.721.3996.0_x64__publisher",
            @"shell:AppsFolder\OpenAI.Codex_publisher!App",
            "26.721.3996.0");
        var current = Candidate(
            "ChatGPT",
            @"C:\Program Files\WindowsApps\OpenAI.Codex_26.721.4979.0_x64__publisher\app\ChatGPT.exe",
            "OpenAI.Codex_26.721.4979.0_x64__publisher",
            @"shell:AppsFolder\OpenAI.Codex_publisher!App",
            "26.721.4979.0");

        var resolved = ApplicationCandidateResolver.ResolveCurrent(
            previous,
            [current]);

        Assert.Same(current, resolved);
    }

    [Fact]
    public void ResolverCollapsesSideBySideStoreVersionsToTheNewest()
    {
        var oldVersion = Candidate(
            "ChatGPT",
            @"C:\Packages\OpenAI.Codex_26.721.3996.0\app\ChatGPT.exe",
            "OpenAI.Codex_26.721.3996.0_x64__publisher",
            @"shell:AppsFolder\OpenAI.Codex_publisher!App",
            "26.721.3996.0");
        var newVersion = Candidate(
            "ChatGPT",
            @"C:\Packages\OpenAI.Codex_26.721.4979.0\app\ChatGPT.exe",
            "OpenAI.Codex_26.721.4979.0_x64__publisher",
            @"shell:AppsFolder\OpenAI.Codex_publisher!App",
            "26.721.4979.0");

        var collapsed = ApplicationCandidateResolver.CollapseVersions(
            [oldVersion, newVersion]);

        Assert.Single(collapsed);
        Assert.Equal("26.721.4979.0", collapsed[0].Version);
    }

    private static ApplicationCandidate Candidate(
        string process,
        string path,
        string? identity,
        string? launchTarget = null,
        string version = "1.0")
    {
        return new ApplicationCandidate(
            process,
            process,
            path,
            launchTarget ?? path,
            identity,
            version,
            null,
            "x64",
            ApplicationLaunchMethod.Executable,
            0,
            false);
    }
}
