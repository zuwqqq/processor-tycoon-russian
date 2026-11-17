using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessorTycoon.Setup;

internal sealed record LatestRelease(string TagName, string Url);

internal static class UpdateChecker
{
    internal const string CurrentVersion = "1.3.0";
    internal const string ReleaseApi = "https://api.github.com/repos/zuwqqq/processor-tycoon-russian/releases/latest";
    internal const string ReleasePage = "https://github.com/zuwqqq/processor-tycoon-russian/releases/latest";

    internal static async Task<LatestRelease?> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        return await CheckLatestReleaseAsync(client, timeout.Token).ConfigureAwait(false);
    }

    internal static async Task<LatestRelease?> CheckLatestReleaseAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseApi);
            request.Headers.UserAgent.ParseAdd($"ProcessorTycoon-Russian-Installer/{CurrentVersion}");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var release = ParseRelease(json);
                if (release != null) return release;
            }
        }
        catch (Exception) { }
        if (cancellationToken.IsCancellationRequested) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasePage);
            request.Headers.UserAgent.ParseAdd($"ProcessorTycoon-Russian-Installer/{CurrentVersion}");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? ParseReleaseLocation(response.RequestMessage?.RequestUri) : null;
        }
        catch (Exception) { return null; }
    }

    internal static LatestRelease? ParseReleaseLocation(Uri? uri)
    {
        const string prefix = "/zuwqqq/processor-tycoon-russian/releases/tag/";
        if (uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var tag = NormalizeVersion(Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]));
        return tag == null ? null : new LatestRelease(tag, ReleasePage);
    }

    internal static LatestRelease? ParseRelease(string json)
    {
        try
        {
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            var tag = NormalizeVersion(release?.TagName);
            return tag == null ? null : new LatestRelease(tag, ReleasePage);
        }
        catch (JsonException) { return null; }
    }

    internal static bool IsNewer(string? candidate, string current = CurrentVersion)
    {
        var left = ParseVersion(candidate);
        var right = ParseVersion(current);
        return left != null && right != null && Compare(left, right) > 0;
    }

    internal static string? NormalizeVersion(string? value)
    {
        var parsed = ParseVersion(value);
        return parsed == null ? null : string.Join('.', parsed);
    }

    private static int[]? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value[1..];
        var parts = value.Split('.');
        if (parts.Length is < 1 or > 4) return null;
        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length is 0 or > 9 || !int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0) return null;
        }
        return numbers;
    }

    private static int Compare(int[] left, int[] right)
    {
        for (var i = 0; i < 4; i++)
            if (left[i] != right[i]) return left[i].CompareTo(right[i]);
        return 0;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    }
}
