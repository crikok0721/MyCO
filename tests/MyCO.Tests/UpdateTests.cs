using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using MyCO.Updates;

namespace MyCO.Tests;

public sealed class UpdateTests
{
    [Fact]
    public async Task ReleaseClientIgnoresDraftsAndPrereleases()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [
                  {"tag_name":"v9.0.0-rc.1","draft":false,"prerelease":true,"published_at":"2026-08-02T01:00:00Z","assets":[]},
                  {"tag_name":"v0.99.3","draft":false,"prerelease":false,"published_at":"2026-08-02T00:00:00Z","body":"Fixes\n\n- safer update","assets":[
                    {"name":"MyCO-win-x64.zip","size":12,"browser_download_url":"https://github.com/crikok0721/MyCO/releases/download/v0.99.3/MyCO-win-x64.zip"},
                    {"name":"MyCO-win-x64.zip.sha256","size":64,"browser_download_url":"https://github.com/crikok0721/MyCO/releases/download/v0.99.3/MyCO-win-x64.zip.sha256"}
                  ]}
                ]
                """,
                Encoding.UTF8,
                "application/json")
        }));
        var service = new GitHubReleaseClient(client, TimeSpan.FromSeconds(2));

        var result = await service.CheckLatestAsync("0.99.2");

        Assert.Equal(UpdateCheckOutcome.Available, result.Outcome);
        Assert.Equal("0.99.3", result.Release!.Version.ToString());
        Assert.Equal("Fixes safer update", result.Release.Summary);
    }

    [Fact]
    public async Task ReleaseClientReportsRateLimitAndInvalidAssetFormat()
    {
        using var limitedClient = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage((HttpStatusCode)429)));
        var limited = await new GitHubReleaseClient(
                limitedClient,
                TimeSpan.FromSeconds(2))
            .CheckLatestAsync("0.99.2");
        Assert.Equal(UpdateCheckOutcome.RateLimited, limited.Outcome);

        using var invalidClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = new StringContent(
                "[{\"tag_name\":\"v0.99.3\",\"draft\":false,\"prerelease\":false,\"assets\":[]}]",
                Encoding.UTF8,
                "application/json")
        }));
        var invalid = await new GitHubReleaseClient(
                invalidClient,
                TimeSpan.FromSeconds(2))
            .CheckLatestAsync("0.99.2");
        Assert.Equal(UpdateCheckOutcome.InvalidFormat, invalid.Outcome);
    }

    [Fact]
    public async Task ReleaseClientSeparatesOfflineTimeoutAndCallerCancellation()
    {
        using var offlineClient = new HttpClient(new OfflineHandler());
        var offline = await new GitHubReleaseClient(
                offlineClient,
                TimeSpan.FromSeconds(2))
            .CheckLatestAsync("0.99.2");
        Assert.Equal(UpdateCheckOutcome.Offline, offline.Outcome);

        using var timeoutClient = new HttpClient(new TimeoutHandler());
        var timeout = await new GitHubReleaseClient(
                timeoutClient,
                TimeSpan.FromMilliseconds(20))
            .CheckLatestAsync("0.99.2");
        Assert.Equal(UpdateCheckOutcome.Timeout, timeout.Outcome);

        using var cancelledClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GitHubReleaseClient(cancelledClient, TimeSpan.FromSeconds(2))
                .CheckLatestAsync("0.99.2", cancellation.Token));
    }

    [Fact]
    public void PackageValidatorRejectsTraversalAndHashMismatch()
    {
        var root = CreateTempDirectory();
        try
        {
            var archive = Path.Combine(root, "malicious.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("..\\outside.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("blocked");
            }

            Assert.Throws<InvalidDataException>(() =>
                UpdatePackageValidator.ExtractAndValidate(
                    archive,
                    Path.Combine(root, "extract")));
            Assert.Throws<InvalidDataException>(() =>
                UpdatePackageValidator.VerifySha256(
                    Encoding.UTF8.GetBytes("actual"),
                    "0000000000000000000000000000000000000000000000000000000000000000"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PackageValidatorRejectsAbsolutePathsAndOversizedHashFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            var archive = Path.Combine(root, "absolute-path.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("/outside.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("blocked");
            }

            Assert.Throws<InvalidDataException>(() =>
                UpdatePackageValidator.ExtractAndValidate(
                    archive,
                    Path.Combine(root, "extract")));
            Assert.Throws<InvalidDataException>(() =>
                UpdatePackageValidator.ReadSha256Text(
                    new byte[(int)UpdatePackageValidator.MaxHashBytes + 1]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PackageValidatorExtractsNormalArchiveUsingActualBytes()
    {
        var root = CreateTempDirectory();
        try
        {
            var archive = Path.Combine(root, "valid.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                var executable = zip.CreateEntry("MyCO.exe");
                await using (var stream = executable.Open())
                await using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    await writer.WriteAsync("safe update");
                }

                var readme = zip.CreateEntry("README.txt");
                await using var readmeStream = readme.Open();
                await using var readmeWriter = new StreamWriter(readmeStream, Encoding.UTF8);
                await readmeWriter.WriteAsync("metadata");
            }

            var manifest = UpdatePackageValidator.ExtractAndValidate(
                archive,
                Path.Combine(root, "extract"));

            Assert.Contains("MyCO.exe", manifest.Files, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("safe update", await File.ReadAllTextAsync(manifest.ExecutablePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExternalInstallerReplacesAndRollsBackOnlyAfterExactProcessExit()
    {
        var root = CreateTempDirectory();
        var cleanup = CreateUpdateCleanupDirectory();
        try
        {
            var install = Path.Combine(root, "install");
            var appData = Path.Combine(root, "AppData");
            var stage = Path.Combine(cleanup, "stage");
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(appData);
            Directory.CreateDirectory(stage);
            await File.WriteAllTextAsync(Path.Combine(install, "MyCO.exe"), "old");
            await File.WriteAllTextAsync(Path.Combine(appData, "config.json"), "keep");
            await File.WriteAllTextAsync(Path.Combine(stage, "MyCO.exe"), "new");
            var expected = Convert.ToHexString(
                    SHA256.HashData(await File.ReadAllBytesAsync(
                        Path.Combine(stage, "MyCO.exe"))))
                .ToLowerInvariant();
            var currentExecutable = Path.Combine(install, "MyCO.exe");
            var probe = new FakeProcessProbe(
                new ProcessIdentity(123, currentExecutable, 77, true),
                new ProcessIdentity(123, currentExecutable, 77, false));
            var installer = new ExternalUpdateInstaller(probe, _ => { });

            await installer.ApplyAsync(
                new UpdateApplyRequest(
                    123,
                    currentExecutable,
                    77,
                    install,
                    stage,
                    expected,
                    LaunchNewProcess: false,
                    CleanupDirectory: cleanup),
                CancellationToken.None);

            Assert.Equal("new", await File.ReadAllTextAsync(
                Path.Combine(install, "MyCO.exe")));
            Assert.Equal("keep", await File.ReadAllTextAsync(
                Path.Combine(appData, "config.json")));
            Assert.DoesNotContain(
                Directory.EnumerateDirectories(root),
                path => path.Contains("backup", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteIfExists(cleanup);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExternalInstallerRestoresOldInstallWhenVerificationFails()
    {
        var root = CreateTempDirectory();
        var cleanup = CreateUpdateCleanupDirectory();
        try
        {
            var install = Path.Combine(root, "install");
            var stage = Path.Combine(cleanup, "stage");
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(stage);
            await File.WriteAllTextAsync(Path.Combine(install, "MyCO.exe"), "old");
            await File.WriteAllTextAsync(Path.Combine(stage, "MyCO.exe"), "tampered");
            var currentExecutable = Path.Combine(install, "MyCO.exe");
            var probe = new FakeProcessProbe(
                new ProcessIdentity(123, currentExecutable, 77, true),
                new ProcessIdentity(123, currentExecutable, 77, false));
            var installer = new ExternalUpdateInstaller(probe, _ => { });

            await Assert.ThrowsAsync<UpdateInstallException>(() => installer.ApplyAsync(
                new UpdateApplyRequest(
                    123,
                    currentExecutable,
                    77,
                    install,
                    stage,
                    "bad-hash",
                    LaunchNewProcess: false,
                    CleanupDirectory: cleanup),
                CancellationToken.None));

            Assert.Equal("old", await File.ReadAllTextAsync(
                Path.Combine(install, "MyCO.exe")));
        }
        finally
        {
            DeleteIfExists(cleanup);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExternalInstallerCleansStagingWhenCancelledBeforeReplacement()
    {
        var root = CreateTempDirectory();
        var cleanup = CreateUpdateCleanupDirectory();
        try
        {
            var install = Path.Combine(root, "install");
            var stage = Path.Combine(cleanup, "stage");
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(stage);
            await File.WriteAllTextAsync(Path.Combine(install, "MyCO.exe"), "old");
            await File.WriteAllTextAsync(Path.Combine(stage, "MyCO.exe"), "new");
            var expected = Convert.ToHexString(
                    SHA256.HashData(await File.ReadAllBytesAsync(
                        Path.Combine(stage, "MyCO.exe"))))
                .ToLowerInvariant();
            var currentExecutable = Path.Combine(install, "MyCO.exe");
            var probe = new FakeProcessProbe(
                new ProcessIdentity(123, currentExecutable, 77, true));
            var installer = new ExternalUpdateInstaller(probe, _ => { });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => installer.ApplyAsync(
                new UpdateApplyRequest(
                    123,
                    currentExecutable,
                    77,
                    install,
                    stage,
                    expected,
                    LaunchNewProcess: false,
                    CleanupDirectory: cleanup),
                cancellation.Token));

            Assert.Equal("old", await File.ReadAllTextAsync(currentExecutable));
            Assert.False(Directory.Exists(cleanup));
        }
        finally
        {
            DeleteIfExists(cleanup);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateUpdateCleanupDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "MyCO",
            "Updates",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MyCO.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The timeout handler should not return.");
        }
    }

    private sealed class FakeProcessProbe(params ProcessIdentity[] identities)
        : IUpdateProcessProbe
    {
        private int _index;

        public ProcessIdentity? Get(int processId)
        {
            var index = Math.Min(_index++, identities.Length - 1);
            return identities[index];
        }
    }
}
