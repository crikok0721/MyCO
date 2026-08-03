using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyCO.Updates;

public enum UpdateCheckOutcome
{
    UpToDate,
    Available,
    Offline,
    Timeout,
    RateLimited,
    InvalidFormat
}

public readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? PreRelease = null) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Regex.Match(
            value.Trim(),
            "^v?(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z.-]+))?(?:\\+[0-9A-Za-z.-]+)?$",
            RegexOptions.CultureInvariant);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor) ||
            !int.TryParse(match.Groups[3].Value, out var patch))
        {
            return false;
        }

        version = new SemanticVersion(
            major,
            minor,
            patch,
            match.Groups[4].Success ? match.Groups[4].Value : null);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0)
        {
            return comparison;
        }
        if (PreRelease is null && other.PreRelease is null)
        {
            return 0;
        }
        if (PreRelease is null)
        {
            return 1;
        }
        if (other.PreRelease is null)
        {
            return -1;
        }
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}{(PreRelease is null ? string.Empty : $"-{PreRelease}")}";

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        for (var index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
        {
            if (index >= leftParts.Length)
            {
                return -1;
            }
            if (index >= rightParts.Length)
            {
                return 1;
            }
            var leftPart = leftParts[index];
            var rightPart = rightParts[index];
            var leftNumeric = int.TryParse(leftPart, out var leftNumber);
            var rightNumeric = int.TryParse(rightPart, out var rightNumber);
            if (leftNumeric && rightNumeric)
            {
                var numericComparison = leftNumber.CompareTo(rightNumber);
                if (numericComparison != 0)
                {
                    return numericComparison;
                }
                continue;
            }
            if (leftNumeric != rightNumeric)
            {
                return leftNumeric ? -1 : 1;
            }
            var textComparison = string.CompareOrdinal(leftPart, rightPart);
            if (textComparison != 0)
            {
                return textComparison;
            }
        }
        return 0;
    }
}

public sealed record ReleaseAsset(
    string Name,
    long Size,
    Uri DownloadUri);

public sealed record OfficialRelease(
    SemanticVersion Version,
    string TagName,
    string Summary,
    ReleaseAsset Archive,
    ReleaseAsset Hash);

public sealed record UpdateCheckResult(
    UpdateCheckOutcome Outcome,
    OfficialRelease? Release = null);

public sealed class GitHubReleaseClient
{
    public const string RepositoryOwner = "crikok0721";
    public const string RepositoryName = "MyCO";
    public const string ArchiveAssetName = "MyCO-win-x64.zip";
    public const string HashAssetName = "MyCO-win-x64.zip.sha256";
    private const string ReleasesEndpoint =
        "https://api.github.com/repos/crikok0721/MyCO/releases?per_page=30";

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public GitHubReleaseClient(
        HttpClient? httpClient = null,
        TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MyCO", "0.99.2"));
        }
    }

    public async Task<UpdateCheckResult> CheckLatestAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!SemanticVersion.TryParse(currentVersion, out var current))
        {
            return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
        }

        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            using var response = await _httpClient.GetAsync(
                    ReleasesEndpoint,
                    HttpCompletionOption.ResponseHeadersRead,
                    linked.Token)
                .ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or
                HttpStatusCode.Forbidden)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.RateLimited);
            }
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.Offline);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(linked.Token)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: linked.Token)
                .ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
            }

            JsonElement? latest = null;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("draft", out var draft) ||
                    !release.TryGetProperty("prerelease", out var prerelease) ||
                    (draft.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) ||
                    (prerelease.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
                {
                    return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
                }
                if (draft.GetBoolean())
                {
                    continue;
                }
                if (prerelease.GetBoolean())
                {
                    continue;
                }
                latest = release;
                break;
            }

            if (latest is null)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.UpToDate);
            }

            var releaseElement = latest.Value;
            var tagName = releaseElement.GetProperty("tag_name").GetString();
            if (!SemanticVersion.TryParse(tagName, out var releaseVersion) ||
                releaseVersion.PreRelease is not null)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
            }
            if (releaseVersion.CompareTo(current) <= 0)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.UpToDate);
            }

            if (!releaseElement.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
            }
            var archive = ReadAsset(assets, ArchiveAssetName);
            var hash = ReadAsset(assets, HashAssetName);
            if (archive is null || hash is null ||
                !IsOfficialDownload(archive.DownloadUri, ArchiveAssetName) ||
                !IsOfficialDownload(hash.DownloadUri, HashAssetName) ||
                archive.Size <= 0 || archive.Size > UpdatePackageValidator.MaxArchiveBytes ||
                hash.Size <= 0 || hash.Size > UpdatePackageValidator.MaxHashBytes)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
            }

            var body = releaseElement.TryGetProperty("body", out var bodyElement)
                ? bodyElement.GetString()
                : null;
            return new UpdateCheckResult(
                UpdateCheckOutcome.Available,
                new OfficialRelease(
                    releaseVersion,
                    tagName!,
                    Summarize(body),
                    archive,
                    hash));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.Timeout);
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.Offline);
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.InvalidFormat);
        }
    }

    private static ReleaseAsset? ReadAsset(JsonElement assets, string expectedName)
    {
        ReleaseAsset? found = null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameElement) ||
                !string.Equals(
                    nameElement.GetString(),
                    expectedName,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (found is not null ||
                !asset.TryGetProperty("size", out var sizeElement) ||
                !sizeElement.TryGetInt64(out var size) ||
                !asset.TryGetProperty("browser_download_url", out var urlElement) ||
                !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var uri))
            {
                return null;
            }
            found = new ReleaseAsset(expectedName, size, uri);
        }
        return found;
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

    private static string Summarize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }
        var plain = Regex.Replace(body, "<[^>]+>", " ");
        plain = Regex.Replace(plain, "[`*_#>-]", " ");
        plain = Regex.Replace(plain, "\\s+", " ").Trim();
        return plain.Length <= 240 ? plain : $"{plain[..237]}...";
    }
}
