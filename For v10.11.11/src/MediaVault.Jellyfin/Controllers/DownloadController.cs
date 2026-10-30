using System.Globalization;
using MediaVault.Jellyfin.Catalogue;
using MediaVault.Jellyfin.Configuration;
using MediaVault.Jellyfin.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaVault.Jellyfin.Controllers;

/// <summary>
/// Client-facing download control API for Media Vault.
///
/// The Jellyfin client talks to this controller. The controller talks to the
/// GPhotosDL HTTP API on the Jellyfin server, so clients never need direct
/// access to port 8282.
/// </summary>
[ApiController]
[Route("MediaVault/Downloads")]
public sealed class DownloadController : ControllerBase
{
    private readonly CatalogueStore _catalogueStore;
    private readonly GPhotosDlService _gphotosDl;

    public DownloadController(
        CatalogueStore catalogueStore,
        GPhotosDlService gphotosDl)
    {
        _catalogueStore = catalogueStore;
        _gphotosDl = gphotosDl;
    }

    [HttpPost("start")]
    public async Task<ActionResult<DownloadItemDto>> Start(
        [FromBody] StartDownloadRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MediaVaultId))
        {
            return BadRequest(new { error = "MediaVaultId is required." });
        }

        var item = FindItem(request.MediaVaultId.Trim());
        if (item is null)
        {
            return NotFound(new { error = "Media Vault item was not found in the catalogue." });
        }

        if (string.IsNullOrWhiteSpace(item.Value.GooglePhotosLink))
        {
            return BadRequest(new { error = "This Media Vault item does not have a Google Photos link." });
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return StatusCode(500, new { error = "Media Vault plugin configuration is unavailable." });
        }

        var directory = Path.GetFullPath(item.Value.Directory);
        Directory.CreateDirectory(directory);

        var expectedPath = Path.Combine(directory, item.Value.Filename);
        if (System.IO.File.Exists(expectedPath))
        {
            return Ok(BuildDto(
                item.Value,
                status: null,
                expectedPath: expectedPath));
        }

        try
        {
            var started = await _gphotosDl.StartAsync(
                    item.Value.MediaVaultId,
                    item.Value.GooglePhotosLink,
                    item.Value.Filename,
                    directory,
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);

            if (started is null)
            {
                return StatusCode(502, new { error = "GPhotosDL did not return a download job." });
            }

            var status = await _gphotosDl
                .GetStatusAsync(
                    item.Value.MediaVaultId,
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(BuildDto(item.Value, status, expectedPath));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StatusCode(
                502,
                new { error = $"Unable to start GPhotosDL download: {ex.Message}" });
        }
    }

    [HttpGet("catalogue")]
    public ActionResult<IReadOnlyList<DownloadCatalogueItemDto>> Catalogue()
    {
        return Ok(GetAllCatalogueItems()
            .Select(item => new DownloadCatalogueItemDto(
                item.MediaVaultId,
                item.Type,
                item.Title,
                item.Filename,
                item.RuntimeMinutes))
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DownloadItemDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return StatusCode(500, new { error = "Media Vault plugin configuration is unavailable." });
        }

        IReadOnlyList<GPhotosDlService.TrackedDownload> tracked;
        try
        {
            tracked = await _gphotosDl
                .GetAllStatusesAsync(
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StatusCode(
                502,
                new { error = $"Unable to query GPhotosDL: {ex.Message}" });
        }

        var result = new List<DownloadItemDto>();

        foreach (var trackedJob in tracked)
        {
            var item = FindItem(trackedJob.MediaVaultId);
            if (item is null)
            {
                continue;
            }

            var expectedPath = Path.Combine(
                Path.GetFullPath(item.Value.Directory),
                item.Value.Filename);

            result.Add(BuildDto(
                item.Value,
                trackedJob.Status,
                expectedPath));
        }

        // Also expose locally cached Media Vault files even if the plugin has
        // not started a GPhotosDL job during this Jellyfin process lifetime.
        foreach (var item in GetAllCatalogueItems())
        {
            if (result.Any(x => string.Equals(
                    x.MediaVaultId,
                    item.MediaVaultId,
                    StringComparison.Ordinal)))
            {
                continue;
            }

            var expectedPath = Path.Combine(
                Path.GetFullPath(item.Directory),
                item.Filename);

            if (System.IO.File.Exists(expectedPath))
            {
                result.Add(BuildDto(item, null, expectedPath));
            }
        }

        return Ok(result
            .OrderBy(x => x.Status is "complete" or "cached" ? 1 : 0)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    [HttpGet("{mediaVaultId}")]
    public async Task<ActionResult<DownloadItemDto>> Get(
        string mediaVaultId,
        CancellationToken cancellationToken)
    {
        var item = FindItem(mediaVaultId);
        if (item is null)
        {
            return NotFound(new { error = "Media Vault item was not found in the catalogue." });
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return StatusCode(500, new { error = "Media Vault plugin configuration is unavailable." });
        }

        var expectedPath = Path.Combine(
            Path.GetFullPath(item.Value.Directory),
            item.Value.Filename);

        try
        {
            var status = await _gphotosDl
                .GetStatusAsync(
                    item.Value.MediaVaultId,
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(BuildDto(item.Value, status, expectedPath));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StatusCode(
                502,
                new { error = $"Unable to query GPhotosDL: {ex.Message}" });
        }
    }

    [HttpDelete("{mediaVaultId}")]
    public async Task<ActionResult<DownloadItemDto>> Cancel(
        string mediaVaultId,
        CancellationToken cancellationToken)
    {
        var item = FindItem(mediaVaultId);
        if (item is null)
        {
            return NotFound(new { error = "Media Vault item was not found in the catalogue." });
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return StatusCode(500, new { error = "Media Vault plugin configuration is unavailable." });
        }

        try
        {
            await _gphotosDl
                .CancelAsync(
                    item.Value.MediaVaultId,
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);

            var expectedPath = Path.Combine(
                Path.GetFullPath(item.Value.Directory),
                item.Value.Filename);

            var status = await _gphotosDl
                .GetStatusAsync(
                    item.Value.MediaVaultId,
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(BuildDto(item.Value, status, expectedPath));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StatusCode(
                502,
                new { error = $"Unable to cancel GPhotosDL download: {ex.Message}" });
        }
    }

    private CatalogueItem? FindItem(string mediaVaultId)
    {
        foreach (var item in GetAllCatalogueItems())
        {
            if (string.Equals(
                    item.MediaVaultId,
                    mediaVaultId,
                    StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private IEnumerable<CatalogueItem> GetAllCatalogueItems()
    {
        foreach (var movie in _catalogueStore.Movies)
        {
            var directory = Plugin.Instance?.Configuration.MoviesDownloadDirectory ?? string.Empty;
            yield return new CatalogueItem(
                movie.MediaVaultId,
                "movie",
                movie.Title,
                movie.GooglePhotosLink,
                movie.RuntimeMinutes,
                movie.Title + ".mkv",
                directory);
        }

        foreach (var show in _catalogueStore.TvShows)
        {
            var root = Plugin.Instance?.Configuration.TvShowsDownloadDirectory ?? string.Empty;

            foreach (var season in show.Seasons)
            {
                var directory = Path.Combine(
                    root,
                    SafePathPart(show.Title),
                    SafePathPart(season.DisplayName));

                foreach (var episode in season.Episodes)
                {
                    var number = episode.DisplayEpisodeNumber
                        ?? episode.EpisodeNumber
                        ?? (episode.DisplayOrder + 1);

                    var filename = $"{number}. {SafePathPart(episode.Title)}.mkv";

                    yield return new CatalogueItem(
                        episode.MediaVaultId,
                        "episode",
                        episode.Title,
                        episode.GooglePhotosLink,
                        episode.RuntimeMinutes,
                        filename,
                        directory);
                }
            }
        }

        foreach (var season in _catalogueStore.F1Seasons)
        {
            var root = Plugin.Instance?.Configuration.F1DownloadDirectory ?? string.Empty;

            foreach (var round in season.Rounds)
            {
                var roundName = FirstNonEmpty(
                    round.Name,
                    round.GrandPrixName,
                    $"Round {round.RoundNumber}");

                var directory = Path.Combine(
                    root,
                    SafePathPart(season.Year.ToString(CultureInfo.InvariantCulture)),
                    SafePathPart(roundName));

                foreach (var @event in round.Events)
                {
                    var filename = SafePathPart(@event.EventType) + ".mkv";

                    yield return new CatalogueItem(
                        @event.MediaVaultId,
                        "f1",
                        @event.EventType,
                        @event.GooglePhotosLink,
                        @event.RuntimeMinutes,
                        filename,
                        directory);
                }
            }
        }
    }

    private static DownloadItemDto BuildDto(
        CatalogueItem item,
        GPhotosDlService.DownloadStatus? status,
        string expectedPath)
    {
        var cached = status?.Status == "complete"
            ? System.IO.File.Exists(expectedPath)
            : status is null && System.IO.File.Exists(expectedPath);

        var downloaded = status?.BytesDownloaded ?? 0;
        var total = status?.TotalBytes ?? 0;
        var progress = status?.Progress ?? (cached ? 100 : 0);

        if (total <= 0 && cached)
        {
            try
            {
                total = new FileInfo(expectedPath).Length;
                downloaded = total;
            }
            catch (IOException)
            {
                // The file may be changing; retain the GPhotosDL values.
            }
        }

        var speedBps = status?.SpeedBytesPerSecond ?? 0;
        var eta = status?.EtaSeconds;

        if (cached)
        {
            progress = 100;
            downloaded = total > 0 ? total : downloaded;
            speedBps = 0;
            eta = 0;
        }

        var cachedRuntimeMinutes = item.RuntimeMinutes is int runtime
            && runtime > 0
            && total > 0
            ? runtime * Math.Clamp((double)downloaded / total, 0, 1)
            : (double?)null;

        var state = cached
            ? "cached"
            : status?.Status ?? "not-downloaded";

        return new DownloadItemDto(
            item.MediaVaultId,
            item.Type,
            item.Title,
            item.Filename,
            state,
            downloaded,
            total,
            Math.Clamp(progress, 0, 100),
            speedBps,
            speedBps / (1024d * 1024d),
            eta,
            item.RuntimeMinutes,
            cachedRuntimeMinutes,
            cachedRuntimeMinutes is double runtimeValue && item.RuntimeMinutes is int runtimeForPercentage
                ? Math.Clamp(runtimeValue / runtimeForPercentage * 100, 0, 100)
                : (double?)null,
            status?.Error);
    }

    private static string SafePathPart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim()
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray();
        var result = new string(chars).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "Unknown" : result;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    public sealed record StartDownloadRequest(string? MediaVaultId);

    public sealed record DownloadCatalogueItemDto(
        string MediaVaultId,
        string Type,
        string Title,
        string Filename,
        int? RuntimeMinutes);

    public sealed record DownloadItemDto(
        string MediaVaultId,
        string Type,
        string Title,
        string Filename,
        string Status,
        long DownloadedBytes,
        long TotalBytes,
        double Progress,
        double SpeedBytesPerSecond,
        double SpeedMbPerSecond,
        double? EtaSeconds,
        int? RuntimeMinutes,
        double? CachedRuntimeMinutes,
        double? CachedRuntimePercentage,
        string? Error);

    private readonly record struct CatalogueItem(
        string MediaVaultId,
        string Type,
        string Title,
        string? GooglePhotosLink,
        int? RuntimeMinutes,
        string Filename,
        string Directory);
}


