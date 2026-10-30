using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class UpdateServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int Calls { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task NewerTag_ReportsUpdateAvailable()
    {
        using var handler = new StubHandler(_ => Json(
            "{\"tag_name\":\"v1.4.0\",\"html_url\":\"https://example.test/v1.4.0\"}"));
        using var service = new UpdateService(handler, "https://example.test/latest");

        var result = await service.CheckAsync(new Version(1, 3, 0));

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(1, 4, 0, 0), result.LatestVersion);
        Assert.Equal("https://example.test/v1.4.0", result.ReleaseUrl);
    }

    [Fact]
    public async Task SameTag_ReportsUpToDate()
    {
        using var handler = new StubHandler(_ => Json(
            "{\"tag_name\":\"v1.3.0\",\"html_url\":\"https://example.test/v1.3.0\"}"));
        using var service = new UpdateService(handler, "https://example.test/latest");

        var result = await service.CheckAsync(new Version(1, 3, 0));

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Equal(new Version(1, 3, 0, 0), result.LatestVersion);
    }

    [Fact]
    public async Task PrereleaseSuffix_IsIgnoredForVersionComparison()
    {
        using var handler = new StubHandler(_ => Json(
            "{\"tag_name\":\"v1.4.0-beta.1\",\"html_url\":\"https://example.test/v1.4.0\"}"));
        using var service = new UpdateService(handler, "https://example.test/latest");

        var result = await service.CheckAsync(new Version(1, 3, 0));

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(1, 4, 0, 0), result.LatestVersion);
    }

    [Fact]
    public async Task HttpError_ReportsFailed()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var service = new UpdateService(handler, "https://example.test/latest");

        var result = await service.CheckAsync(new Version(1, 3, 0));

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task MalformedJson_ReportsFailed()
    {
        using var handler = new StubHandler(_ => Json("{not json"));
        using var service = new UpdateService(handler, "https://example.test/latest");

        var result = await service.CheckAsync(new Version(1, 3, 0));

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Theory]
    [InlineData("v1.4.0", "1.4.0.0")]
    [InlineData("1.4", "1.4.0.0")]
    [InlineData("V1.4.0-beta.2", "1.4.0.0")]
    [InlineData("v1.4.0+build", "1.4.0.0")]
    public void ParseVersion_NormalizesCommonTags(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateService.ParseVersion(tag));
    }

    [Fact]
    public void ParseVersion_RejectsInvalidTag()
    {
        Assert.Null(UpdateService.ParseVersion("latest"));
    }
}
