using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScreenShade.App;

internal static partial class UpdateChecker
{
    private static readonly HttpClient Client = CreateClient();

    public static async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(AppInfo.LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return UpdateCheckResult.NoRelease(AppInfo.CurrentVersion);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var tagName = GetString(root, "tag_name");
        var releaseName = GetString(root, "name");
        var releaseUrl = GetString(root, "html_url") ?? AppInfo.ReleasesUrl;
        var publishedAt = GetDateTime(root, "published_at");
        var assets = GetAssets(root);

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new InvalidOperationException("GitHub Release 响应缺少版本标签。");
        }

        var currentVersion = AppInfo.CurrentVersion;
        var isNewer = ReleaseVersion.TryParse(tagName, out var latest)
            && ReleaseVersion.TryParse(currentVersion, out var current)
            && latest.CompareTo(current) > 0;

        return new UpdateCheckResult(
            currentVersion,
            tagName,
            releaseName,
            releaseUrl,
            publishedAt,
            assets,
            isNewer,
            false);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? GetDateTime(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(property.GetString(), out var value)
                ? value
                : null;
    }

    private static IReadOnlyList<ReleaseAsset> GetAssets(JsonElement element)
    {
        if (!element.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var assets = new List<ReleaseAsset>();
        foreach (var assetElement in assetsElement.EnumerateArray())
        {
            var name = GetString(assetElement, "name");
            var downloadUrl = GetString(assetElement, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var size = assetElement.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var value)
                ? value
                : 0;

            assets.Add(new ReleaseAsset(name, downloadUrl, size));
        }

        return assets;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.CurrentVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private readonly partial record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
    {
        public int CompareTo(ReleaseVersion other)
        {
            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0)
            {
                return majorComparison;
            }

            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0)
            {
                return minorComparison;
            }

            return Patch.CompareTo(other.Patch);
        }

        public static bool TryParse(string value, out ReleaseVersion version)
        {
            version = default;
            var match = VersionPattern().Match(value.Trim());
            if (!match.Success)
            {
                return false;
            }

            version = new ReleaseVersion(
                int.Parse(match.Groups["major"].Value),
                ParseOptionalVersionPart(match.Groups["minor"]),
                ParseOptionalVersionPart(match.Groups["patch"]));
            return true;
        }

        private static int ParseOptionalVersionPart(Group group)
        {
            return group.Success && !string.IsNullOrEmpty(group.Value)
                ? int.Parse(group.Value)
                : 0;
        }

        [GeneratedRegex(@"^v?(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?(?:[-+].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex VersionPattern();
    }
}

internal sealed record ReleaseAsset(
    string Name,
    string DownloadUrl,
    long Size);

internal sealed record UpdateCheckResult(
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseName,
    string? ReleaseUrl,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ReleaseAsset> Assets,
    bool IsUpdateAvailable,
    bool ReleaseNotFound)
{
    public static UpdateCheckResult NoRelease(string currentVersion)
    {
        return new UpdateCheckResult(currentVersion, null, null, null, null, [], false, true);
    }
}
