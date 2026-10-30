using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MediaVault.Jellyfin.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaVault.Jellyfin.Services;

/// <summary>
/// Thin HTTP client for the external GPhotosDL service.
/// GPhotosDL owns Google authentication and the actual media download.
/// </summary>
public sealed class GPhotosDlService
{
    private static readonly HttpClient HttpClient = new();
    private readonly ILogger<GPhotosDlService> _logger;
    private readonly ConcurrentDictionary<string, DownloadJob> _jobs = new(StringComparer.Ordinal);

    public GPhotosDlService(ILogger<GPhotosDlService> logger)
    {
        _logger = logger;
    }

    public async Task<DownloadStartResult?> StartAsync(
        string mediaVaultId,
        string shareUrl,
        string filename,
        string directory,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shareUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("A download directory is not configured.");
        }

        if (_jobs.TryGetValue(mediaVaultId, out var existing)
            && !IsTerminal(existing.Status))
        {
            return new DownloadStartResult(existing.Id, existing.Filename, existing.Status);
        }

        var baseUrl = GetBaseUrl(Plugin.Instance?.Configuration.GPhotosDlUrl);
        if (baseUrl is null)
        {
            throw new InvalidOperationException("GPhotosDL server URL is not configured correctly.");
        }

        using var timeout = CreateTimeoutToken(timeoutSeconds, cancellationToken);
        var payload = new DownloadRequest(shareUrl, filename, directory);

        using var response = await HttpClient.PostAsJsonAsync(
            new Uri(baseUrl, "download"),
            payload,
            timeout.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"GPhotosDL download request failed with HTTP {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<DownloadStartResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result is null || !result.Success || string.IsNullOrWhiteSpace(result.Id))
        {
            throw new InvalidOperationException("GPhotosDL returned an invalid download response.");
        }

        var job = new DownloadJob(
            result.Id,
            result.Filename ?? filename,
            result.Status ?? "starting");

        _jobs[mediaVaultId] = job;

        _logger.LogInformation(
            "Started GPhotosDL download for Media Vault item {MediaVaultId}: {DownloadId} -> {Directory}\\{Filename}",
            mediaVaultId,
            job.Id,
            directory,
            job.Filename);

        return new DownloadStartResult(job.Id, job.Filename, job.Status);
    }

    public async Task<DownloadStatus?> GetStatusAsync(
        string mediaVaultId,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(mediaVaultId, out var job))
        {
            return null;
        }

        return await GetStatusForJobAsync(mediaVaultId, job, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the current GPhotosDL status for every job known to this
    /// Jellyfin plugin instance. GPhotosDL remains the source of truth for
    /// byte-level progress.
    /// </summary>
    public async Task<IReadOnlyList<TrackedDownload>> GetAllStatusesAsync(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var jobs = _jobs.ToArray();
        if (jobs.Length == 0)
        {
            return [];
        }

        var results = await Task.WhenAll(
                jobs.Select(async pair =>
                {
                    var status = await GetStatusForJobAsync(
                            pair.Key,
                            pair.Value,
                            timeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return new TrackedDownload(pair.Key, status);
                }))
            .ConfigureAwait(false);

        return results
            .OrderBy(x => x.Status?.Status is "complete" ? 1 : 0)
            .ThenBy(x => x.Status?.Filename ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task CancelAsync(
        string mediaVaultId,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(mediaVaultId, out var job))
        {
            return;
        }

        var baseUrl = GetBaseUrl(Plugin.Instance?.Configuration.GPhotosDlUrl);
        if (baseUrl is null)
        {
            throw new InvalidOperationException("GPhotosDL server URL is not configured correctly.");
        }

        using var timeout = CreateTimeoutToken(timeoutSeconds, cancellationToken);
        using var response = await HttpClient.DeleteAsync(
            new Uri(baseUrl, $"download/{Uri.EscapeDataString(job.Id)}"),
            timeout.Token).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        _jobs[mediaVaultId] = job with { Status = "cancelled" };

        _logger.LogInformation(
            "Cancelled GPhotosDL download {DownloadId} for Media Vault item {MediaVaultId}.",
            job.Id,
            mediaVaultId);
    }

    public bool HasTrackedJob(string mediaVaultId) =>
        _jobs.ContainsKey(mediaVaultId);

    public string GetExpectedPath(string directory, string filename) =>
        Path.Combine(directory, filename);

    private async Task<DownloadStatus?> GetStatusForJobAsync(
        string mediaVaultId,
        DownloadJob job,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var baseUrl = GetBaseUrl(Plugin.Instance?.Configuration.GPhotosDlUrl);
        if (baseUrl is null)
        {
            return null;
        }

        using var timeout = CreateTimeoutToken(timeoutSeconds, cancellationToken);
        using var response = await HttpClient.GetAsync(
            new Uri(baseUrl, $"download/{Uri.EscapeDataString(job.Id)}"),
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var status = await response.Content
            .ReadFromJsonAsync<DownloadStatus>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (status is not null)
        {
            _jobs[mediaVaultId] = job with
            {
                Filename = status.Filename ?? job.Filename,
                Status = status.Status ?? job.Status
            };
        }

        return status;
    }

    private static CancellationTokenSource CreateTimeoutToken(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(
            timeoutSeconds <= 0 ? 30 : timeoutSeconds,
            1,
            300);

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(timeout));
        return linked;
    }

    private static Uri? GetBaseUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        var trimmedUrl = rawUrl.Trim();

        // Ensure the protocol prefix is present if omitted
        if (!trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmedUrl = "http://" + trimmedUrl;
        }

        // Ensure trailing slash for proper relative Uri construction
        if (!trimmedUrl.EndsWith('/'))
        {
            trimmedUrl += "/";
        }

        return Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static bool IsTerminal(string status) =>
        status is "complete" or "failed" or "cancelled";

    private sealed record DownloadRequest(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("directory")] string Directory);

    private sealed record DownloadStartResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("filename")] string? Filename,
        [property: JsonPropertyName("status")] string? Status);

    public sealed record DownloadStatus(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("filename")] string? Filename,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("bytes_downloaded")] long BytesDownloaded,
        [property: JsonPropertyName("total_bytes")] long TotalBytes,
        [property: JsonPropertyName("progress")] double Progress,
        [property: JsonPropertyName("speed_bytes_per_second")] double SpeedBytesPerSecond,
        [property: JsonPropertyName("speed_mb_per_second")] double SpeedMbPerSecond,
        [property: JsonPropertyName("eta_seconds")] double? EtaSeconds,
        [property: JsonPropertyName("error")] string? Error);

    public sealed record DownloadStartResult(string Id, string Filename, string Status);

    public sealed record TrackedDownload(
        string MediaVaultId,
        DownloadStatus? Status);

    private sealed record DownloadJob(
        string Id,
        string Filename,
        string Status);
}