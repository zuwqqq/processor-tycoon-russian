using System.Net;
using ProcessorTycoon.Setup;

var assertions = 0;
void Check(bool condition, string name)
{
    assertions++;
    if (!condition) throw new Exception(name);
}

Check(UpdateChecker.NormalizeVersion("v1.2") == "1.2.0.0", "normalize short version");
Check(UpdateChecker.IsNewer("1.2.1", "1.2.0"), "new patch version");
Check(!UpdateChecker.IsNewer("1.2", "1.2.0"), "same normalized version");
Check(!UpdateChecker.IsNewer("1.1.99", "1.2.0"), "old version");
Check(UpdateChecker.IsNewer(UpdateChecker.CurrentVersion, "1.2.0"), "new release updates previous installer");
Check(!UpdateChecker.IsNewer(UpdateChecker.CurrentVersion), "current installer does not update itself");
Check(UpdateChecker.IsNewer("1.10.0", "1.9.9"), "numeric rather than lexical comparison");
Check(!UpdateChecker.IsNewer("1."), "invalid version");
Check(!UpdateChecker.IsNewer("999999999999999999999"), "huge version");
Check(UpdateChecker.ParseRelease("{\"tag_name\":\"v1.2\",\"html_url\":\"https://evil.example\"}")?.Url == UpdateChecker.ReleasePage, "trusted URL");
Check(UpdateChecker.ParseRelease("{\"tag_name\":\"not-a-version\"}") == null, "invalid tag");
Check(UpdateChecker.ParseRelease("{") == null, "malformed JSON");
Check(UpdateChecker.ParseRelease("{}") == null, "missing release tag");
Check(UpdateChecker.ParseReleaseLocation(new Uri("https://github.com/zuwqqq/processor-tycoon-russian/releases/tag/v1.3.0"))?.TagName == "1.3.0.0", "official latest redirect");
foreach (var location in new[]
{
    "https://evil.example/zuwqqq/processor-tycoon-russian/releases/tag/v9.0.0",
    "https://github.com/other/repo/releases/tag/v9.0.0",
    "http://github.com/zuwqqq/processor-tycoon-russian/releases/tag/v9.0.0",
    "https://github.com/zuwqqq/processor-tycoon-russian/releases/latest",
    "https://github.com/zuwqqq/processor-tycoon-russian/releases/tag/invalid"
})
    Check(UpdateChecker.ParseReleaseLocation(new Uri(location)) == null, "reject untrusted or invalid redirect " + location);

var fallbackCalls = 0;
using (var client = new HttpClient(new StubHandler((request, _) =>
{
    fallbackCalls++;
    if (request.RequestUri?.AbsoluteUri == UpdateChecker.ReleaseApi)
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
    Check(request.RequestUri?.AbsoluteUri == UpdateChecker.ReleasePage, "fallback uses official latest page");
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://github.com/zuwqqq/processor-tycoon-russian/releases/tag/v1.3.0")
    });
})))
{
    var latest = await UpdateChecker.CheckLatestReleaseAsync(client);
    Check(latest?.TagName == "1.3.0.0" && latest.Url == UpdateChecker.ReleasePage, "GitHub API limit recovered through latest-page redirect");
    Check(fallbackCalls == 2, "exactly one fallback request");
}


using (var client = new HttpClient(new StubHandler((request, _) =>
{
    Check(request.Method == HttpMethod.Get, "read-only HTTP method");
    Check(request.RequestUri?.AbsoluteUri == UpdateChecker.ReleaseApi, "official GitHub API endpoint");
    Check(request.Headers.UserAgent.ToString() == $"ProcessorTycoon-Russian-Installer/{UpdateChecker.CurrentVersion}", "versioned user agent");
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"tag_name\":\"v1.3.0\",\"html_url\":\"https://evil.example\"}")
    });
})))
{
    var latest = await UpdateChecker.CheckLatestReleaseAsync(client);
    Check(latest != null && UpdateChecker.IsNewer(latest.TagName, "1.2.0"), "HTTP response triggers update for old installer");
    Check(latest != null && !UpdateChecker.IsNewer(latest.TagName, "1.3.0"), "HTTP response does not update current installer");
    Check(latest?.Url == UpdateChecker.ReleasePage, "HTTP response cannot override download URL");
}

foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
{
    using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
    Check(await UpdateChecker.CheckLatestReleaseAsync(client) == null, $"nonblocking GitHub error {status}");
}
using (var client = new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("Offline"))))
    Check(await UpdateChecker.CheckLatestReleaseAsync(client) == null, "network failure is nonfatal");
using (var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json") }))))
    Check(await UpdateChecker.CheckLatestReleaseAsync(client) == null, "malformed HTTP response is nonfatal");
using (var client = new HttpClient(new StubHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token))))
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    Check(await UpdateChecker.CheckLatestReleaseAsync(client, cancellation.Token) == null, "cancelled request is nonfatal");
}
using (var client = new HttpClient(new StubHandler(async (_, token) =>
{
    await Task.Delay(Timeout.InfiniteTimeSpan, token);
    return new HttpResponseMessage(HttpStatusCode.OK);
})) { Timeout = TimeSpan.FromMilliseconds(100) })
    Check(await UpdateChecker.CheckLatestReleaseAsync(client) == null, "request timeout is nonfatal");

Console.WriteLine($"PASS: {assertions} update checker assertions");
if (args.Contains("--live"))
{
    var latest = await UpdateChecker.CheckLatestReleaseAsync();
    Check(latest != null, "live GitHub latest release must be available");
    Console.WriteLine($"LIVE: current={UpdateChecker.CurrentVersion}; latest={latest!.TagName}; old-1.2.0-update={UpdateChecker.IsNewer(latest.TagName, "1.2.0")}; current-update={UpdateChecker.IsNewer(latest.TagName)}; url={latest.Url}");
}

sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
}
