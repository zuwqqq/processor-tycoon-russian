using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessorTycoon.Setup;

internal sealed record LatestRelease(string TagName, string Url);

internal static class UpdateChecker
{
    internal const string CurrentVersion = "1.2.0";
    internal const string ReleaseApi = "https://api.github.com/repos/zuwqqq/processor-tycoon-russian/releases/latest";
    internal const string ReleasePage = "https://github.com/zuwqqq/processor-tycoon-russian/releases/latest";

    internal static async Task<LatestRelease?> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ProcessorTycoon-Russian-Installer/1.2.0");
            using var response = await client.GetAsync(ReleaseApi, cancellationToken).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
            return ParseRelease(json);
        }
        catch (Exception) { return null; }
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
