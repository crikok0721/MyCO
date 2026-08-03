using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MyCO.Updates;

namespace MyCO.Manager.Services;

internal sealed record PreparedUpdate(
    UpdateApplyRequest Request,
    string UpdaterPath,
    string WorkingDirectory);

internal sealed class UpdateCoordinator : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GitHubReleaseClient _releaseClient;
    private bool _disposed;

    public UpdateCoordinator()
    {
        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MyCO", MyCO.BuildInfo.Version));
        _releaseClient = new GitHubReleaseClient(_httpClient);
    }

    public Task<UpdateCheckResult> CheckLatestAsync(
        CancellationToken cancellationToken = default) =>
        _releaseClient.CheckLatestAsync(
            MyCO.BuildInfo.Version,
            cancellationToken);

    public async Task<PreparedUpdate> PrepareAsync(
        OfficialRelease release,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateRelease(release);
        var temporaryMycoRoot = Path.Combine(Path.GetTempPath(), "MyCO");
        var updatesRoot = Path.Combine(temporaryMycoRoot, "Updates");
        Directory.CreateDirectory(temporaryMycoRoot);
        EnsureNoReparsePoint(temporaryMycoRoot);
        Directory.CreateDirectory(updatesRoot);
        EnsureNoReparsePoint(updatesRoot);
        var updateRoot = Path.Combine(
            updatesRoot,
            Guid.NewGuid().ToString("N"));
        var stage = Path.Combine(updateRoot, "stage");
        var updaterDirectory = Path.Combine(updateRoot, "updater");
        var archivePath = Path.Combine(updateRoot, GitHubReleaseClient.ArchiveAssetName);
        var hashPath = Path.Combine(updateRoot, GitHubReleaseClient.HashAssetName);
        Directory.CreateDirectory(updateRoot);
        try
        {
            EnsureNoReparsePoint(updateRoot);
            await DownloadFileAsync(
                    release.Archive.DownloadUri,
                    archivePath,
                    GitHubReleaseClient.ArchiveAssetName,
                    UpdatePackageValidator.MaxArchiveBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            await DownloadFileAsync(
                    release.Hash.DownloadUri,
                    hashPath,
                    GitHubReleaseClient.HashAssetName,
                    UpdatePackageValidator.MaxHashBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            var expectedArchiveHash = UpdatePackageValidator.ReadSha256Text(
                await File.ReadAllBytesAsync(hashPath, cancellationToken).ConfigureAwait(false));
            await VerifyFileHashAsync(
                    archivePath,
                    expectedArchiveHash,
                    cancellationToken)
                .ConfigureAwait(false);
            var manifest = UpdatePackageValidator.ExtractAndValidate(archivePath, stage);
            var currentExecutable = Environment.ProcessPath
                                    ?? throw new InvalidOperationException(
                                        "The MyCO executable path is unavailable.");
            var installDirectory = Path.GetDirectoryName(
                                       Path.GetFullPath(currentExecutable))
                                   ?? throw new InvalidOperationException(
                                       "The MyCO installation directory is unavailable.");
            var updaterSource = Path.Combine(installDirectory, "MyCO.Updater.exe");
            if (!File.Exists(updaterSource) ||
                (File.GetAttributes(updaterSource) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UpdateInstallException(
                    "The project updater is not present in this installation.");
            }
            Directory.CreateDirectory(updaterDirectory);
            var updaterPath = Path.Combine(updaterDirectory, "MyCO.Updater.exe");
            File.Copy(updaterSource, updaterPath, overwrite: false);
            using var process = Process.GetCurrentProcess();
            var startTimeTicks = process.StartTime.ToUniversalTime().Ticks;
            var request = new UpdateApplyRequest(
                process.Id,
                Path.GetFullPath(currentExecutable),
                startTimeTicks,
                installDirectory,
                stage,
                manifest.ExecutableSha256,
                LaunchNewProcess: true,
                WaitTimeout: TimeSpan.FromMinutes(5),
                CleanupDirectory: updateRoot);
            return new PreparedUpdate(request, updaterPath, updateRoot);
        }
        catch
        {
            TryDeleteDirectory(updateRoot);
            throw;
        }
    }

    public Process Launch(PreparedUpdate prepared)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var request = prepared.Request;
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = prepared.UpdaterPath,
                WorkingDirectory = prepared.WorkingDirectory,
                UseShellExecute = true,
                ArgumentList =
                {
                    "--apply-update",
                    "--pid", request.ProcessId.ToString(CultureInfo.InvariantCulture),
                    "--path", request.CurrentProcessPath,
                    "--start-ticks", request.CurrentProcessStartTimeUtcTicks
                        .ToString(CultureInfo.InvariantCulture),
                    "--install-dir", request.InstallDirectory,
                    "--stage", request.StagedDirectory,
                    "--expected-sha256", request.ExpectedExecutableSha256,
                    "--cleanup", request.CleanupDirectory ?? string.Empty
                }
            });
            return process ?? throw new InvalidOperationException(
                "The project updater could not be started.");
        }
        catch
        {
            if (request.CleanupDirectory is not null)
            {
                TryDeleteDirectory(request.CleanupDirectory);
            }
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _httpClient.Dispose();
    }

    private static void ValidateRelease(OfficialRelease release)
    {
        if (!string.Equals(
                release.Archive.Name,
                GitHubReleaseClient.ArchiveAssetName,
                StringComparison.Ordinal) ||
            !string.Equals(
                release.Hash.Name,
                GitHubReleaseClient.HashAssetName,
                StringComparison.Ordinal) ||
            release.Archive.Size <= 0 ||
            release.Archive.Size > UpdatePackageValidator.MaxArchiveBytes ||
            release.Hash.Size <= 0 ||
            release.Hash.Size > UpdatePackageValidator.MaxHashBytes ||
            !IsOfficialDownload(
                release.Archive.DownloadUri,
                GitHubReleaseClient.ArchiveAssetName) ||
            !IsOfficialDownload(
                release.Hash.DownloadUri,
                GitHubReleaseClient.HashAssetName))
        {
            throw new InvalidDataException("The official update assets are not valid.");
        }
    }

    private async Task DownloadFileAsync(
        Uri uri,
        string destination,
        string expectedAssetName,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (!IsOfficialDownload(uri, expectedAssetName))
        {
            throw new InvalidDataException("The update download address is not official.");
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        using var response = await _httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                linked.Token)
            .ConfigureAwait(false);
        if (response.StatusCode is System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.Forbidden)
        {
            throw new UpdateInstallException("GitHub temporarily limited the download.");
        }
        response.EnsureSuccessStatusCode();
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is not null && contentLength.Value > maximumBytes)
        {
            throw new InvalidDataException("The update download exceeds the size limit.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(linked.Token)
            .ConfigureAwait(false);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("The update download exceeds the size limit.");
            }
            await output.WriteAsync(buffer.AsMemory(0, read), linked.Token)
                .ConfigureAwait(false);
        }
    }

    private static async Task VerifyFileHashAsync(
        string filePath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var actual = await sha.ComputeHashAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        var expected = Convert.FromHexString(expectedHash);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new InvalidDataException("The downloaded update hash does not match.");
        }
    }

    private static bool IsOfficialDownload(Uri uri, string expectedAssetName)
    {
        const string prefix = "/crikok0721/MyCO/releases/download/";
        var path = uri.AbsolutePath.TrimEnd('/');
        var lastSlash = path.LastIndexOf('/');
        return uri.Scheme == Uri.UriSchemeHttps &&
               string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
               path.StartsWith(prefix, StringComparison.Ordinal) &&
               lastSlash > prefix.Length &&
               string.Equals(
                   path[(lastSlash + 1)..],
                   expectedAssetName,
                   StringComparison.Ordinal);
    }

    private static void EnsureNoReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The private update area contains a reparse point.");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
