using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KayoneZSoundpad.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version? CurrentVersion,
    Version? LatestVersion,
    string? ReleaseUrl,
    string? Error);

/// <summary>
/// GitHub Releases üzerinden son sürümü kontrol eder. Token kullanmaz; public repo.
/// Ağ hatası veya bozuk yanıt uygulamayı etkilemez, Failed sonucu döner.
/// </summary>
public sealed class UpdateService : IDisposable
{
    public const string DefaultApiUrl = "https://api.github.com/repos/ObsQrr/Voxora/releases/latest";

    private readonly HttpClient _http;
    private readonly string _apiUrl;
    private bool _disposed;

    public UpdateService(HttpMessageHandler? handler = null, string? apiUrl = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Voxora/" + GetCurrentVersion().ToString(3));
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _apiUrl = apiUrl ?? DefaultApiUrl;
    }

    public static Version GetCurrentVersion()
        => Normalize(typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0));

    public async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        current = Normalize(current);
        try
        {
            using var response = await _http.GetAsync(_apiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Failed(current, $"HTTP {(int)response.StatusCode}");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<ReleaseInfo>(
                stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (release?.TagName is null || release.HtmlUrl is null)
                return Failed(current, "Invalid response");

            var latest = ParseVersion(release.TagName);
            if (latest is null)
                return Failed(current, "Invalid version tag");

            return latest > current
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, current, latest, release.HtmlUrl, null)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, current, latest, release.HtmlUrl, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(current, "Cancelled");
        }
        catch (Exception ex)
        {
            return Failed(current, ex.Message);
        }
    }

    private static UpdateCheckResult Failed(Version current, string error)
        => new(UpdateCheckStatus.Failed, current, null, null, error);

    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    internal static Version? ParseVersion(string tag)
    {
        var value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];

        // v1.4.0-beta.1 / v1.4.0+meta → yalnızca sayısal sürüm
        var cut = value.IndexOfAny(new[] { '-', '+' });
        if (cut > 0) value = value[..cut];

        return Version.TryParse(value, out var version) ? Normalize(version) : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }

    private sealed class ReleaseInfo
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    }
}
