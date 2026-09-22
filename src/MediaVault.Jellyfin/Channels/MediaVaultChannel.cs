using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaVault.Jellyfin.Catalogue;
using MediaVault.Jellyfin.Configuration;
using MediaVault.Jellyfin.Services;
using Microsoft.Extensions.Logging;

namespace MediaVault.Jellyfin.Channels;

/// <summary>
/// A file-less Cloud Movies catalogue using Jellyfin's supported Channel API.
///
/// The channel is deliberately catalogue-only.
///
/// When a movie is played:
///
/// 1. The plugin checks whether the movie already exists in Jellyfin's normal
///    local Movies library.
/// 2. If it does, no new GPhotosDL download is started.
/// 3. If it does not, GPhotosDL is started/reused.
/// 4. The plugin waits only until the local file exists and is readable enough
///    for Jellyfin to inspect it. It does NOT wait for the complete download.
/// 5. The plugin resolves that exact local filesystem path through Jellyfin's
///    normal resolver pipeline.
/// 6. If necessary, the resolved BaseItem is registered with Jellyfin directly.
/// 7. The plugin registers/resolves that exact item without initiating a metadata refresh or whole-library scan.
///    initiating a whole-library scan.
/// 8. The plugin waits for Jellyfin to produce a usable normal media source.
/// 9. The REAL MediaSourceInfo from Jellyfin's normal library item is returned.
///
/// The channel does not manufacture its own MediaSourceInfo.
///
/// The purpose of this architecture is to make a Media Vault movie behave
/// exactly like the corresponding file in Jellyfin's normal Movies library,
/// while avoiding an expensive whole-library scan on every on-demand play.
///
/// GPhotosDL remains responsible only for obtaining the local file.
/// Jellyfin remains responsible for resolving, probing and playing the file.
/// </summary>
public enum MediaVaultChannelKind
{
    Movies,
    TvShows,
    Formula1
}

/// <summary>
/// Distinct Jellyfin channel type for the Media Vault movie catalogue.
/// </summary>
public sealed class MediaVaultMoviesChannel : MediaVaultChannel
{
    public MediaVaultMoviesChannel(
        CatalogueStore catalogueStore,
        GPhotosDlService gphotosDl,
        ILogger<MediaVaultChannel> logger,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IMediaEncoder mediaEncoder,
        IMediaStreamRepository mediaStreamRepository)
        : base(
            catalogueStore,
            gphotosDl,
            logger,
            libraryManager,
            mediaSourceManager,
            mediaEncoder,
            mediaStreamRepository,
            MediaVaultChannelKind.Movies)
    {
    }
}

/// <summary>
/// Distinct Jellyfin channel type for the Media Vault TV catalogue.
/// </summary>
public sealed class MediaVaultTvShowsChannel : MediaVaultChannel
{
    public MediaVaultTvShowsChannel(
        CatalogueStore catalogueStore,
        GPhotosDlService gphotosDl,
        ILogger<MediaVaultChannel> logger,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IMediaEncoder mediaEncoder,
        IMediaStreamRepository mediaStreamRepository)
        : base(
            catalogueStore,
            gphotosDl,
            logger,
            libraryManager,
            mediaSourceManager,
            mediaEncoder,
            mediaStreamRepository,
            MediaVaultChannelKind.TvShows)
    {
    }
}

/// <summary>
/// Distinct Jellyfin channel type for the Media Vault Formula 1 catalogue.
/// </summary>
public sealed class MediaVaultFormula1Channel : MediaVaultChannel
{
    public MediaVaultFormula1Channel(
        CatalogueStore catalogueStore,
        GPhotosDlService gphotosDl,
        ILogger<MediaVaultChannel> logger,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IMediaEncoder mediaEncoder,
        IMediaStreamRepository mediaStreamRepository)
        : base(
            catalogueStore,
            gphotosDl,
            logger,
            libraryManager,
            mediaSourceManager,
            mediaEncoder,
            mediaStreamRepository,
            MediaVaultChannelKind.Formula1)
    {
    }
}

public class MediaVaultChannel : IChannel, IRequiresMediaInfoCallback
{
    private const int DefaultLibraryDiscoveryWaitSeconds = 60;

    private const int LibraryDiscoveryPollMilliseconds = 500;

    private const int MediaSourcePollMilliseconds = 500;

    private const string MovieExtension = ".mkv";

    /// <summary>
    /// We do not ask Jellyfin to inspect a zero-byte/tiny file.
    ///
    /// This is deliberately small. We are NOT waiting for the download to
    /// complete and we are NOT waiting for a large percentage of the movie.
    /// </summary>
    private const long MinimumBytesBeforeLibraryInspection = 8L * 1024L * 1024L;

    /// <summary>
    /// Number of consecutive successful read checks required before handing
    /// the file to Jellyfin.
    /// </summary>
    private const int RequiredReadableChecks = 2;

    private readonly CatalogueStore _catalogueStore;

    private readonly GPhotosDlService _gphotosDl;

    private readonly ILogger<MediaVaultChannel> _logger;

    private readonly ILibraryManager _libraryManager;

    private readonly IMediaSourceManager _mediaSourceManager;

    private readonly IMediaEncoder _mediaEncoder;

    private readonly IMediaStreamRepository _mediaStreamRepository;

    private readonly MediaVaultChannelKind _channelKind;

    public MediaVaultChannel(
        CatalogueStore catalogueStore,
        GPhotosDlService gphotosDl,
        ILogger<MediaVaultChannel> logger,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IMediaEncoder mediaEncoder,
        IMediaStreamRepository mediaStreamRepository,
        MediaVaultChannelKind channelKind)
    {
        _catalogueStore = catalogueStore;
        _gphotosDl = gphotosDl;
        _logger = logger;
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _mediaEncoder = mediaEncoder;
        _mediaStreamRepository = mediaStreamRepository;
        _channelKind = channelKind;
    }

    public string Name => _channelKind switch
    {
        MediaVaultChannelKind.Movies => "Cinedex Movies",
        MediaVaultChannelKind.TvShows => "Cloud TV Shows",
        MediaVaultChannelKind.Formula1 => "Formula 1",
        _ => "Media Vault"
    };

    public string Description => _channelKind switch
    {
        MediaVaultChannelKind.Movies =>
            "Media Vault movies with on-demand GPhotosDL downloads.",
        MediaVaultChannelKind.TvShows =>
            "Media Vault TV shows and episodes with on-demand GPhotosDL downloads.",
        MediaVaultChannelKind.Formula1 =>
            "Media Vault Formula 1 seasons, rounds and events with on-demand GPhotosDL downloads.",
        _ => "Media Vault cloud media with on-demand GPhotosDL downloads."
    };

    public string DataVersion =>
        _catalogueStore.Status.Revision
            .ToString(CultureInfo.InvariantCulture)
        + "-targeted-media-probe-v14-sort-title-f1-event-display-original-title-"
        + _channelKind.ToString().ToLowerInvariant();

    public string HomePageUrl => string.Empty;

    public ChannelParentalRating ParentalRating =>
        ChannelParentalRating.GeneralAudience;

    public InternalChannelFeatures GetChannelFeatures() =>
        new()
        {
            MediaTypes = [ChannelMediaType.Video],
            ContentTypes = _channelKind == MediaVaultChannelKind.TvShows
                ? [ChannelMediaContentType.Episode]
                : [ChannelMediaContentType.Movie],
            DefaultSortFields = [ChannelItemSortField.Name],
            SupportsSortOrderToggle = true
        };

    public bool IsEnabledFor(string userId) => true;

    public Task<ChannelItemResult> GetChannelItems(
        InternalChannelItemQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderId = query?.FolderId;

        var items = folderId switch
        {
            null or "" => RootItems(),

            ChannelItemFactory.CloudMoviesFolderId
                when _channelKind == MediaVaultChannelKind.Movies
                => MovieItems(),

            ChannelItemFactory.CloudTvShowsFolderId
                when _channelKind == MediaVaultChannelKind.TvShows
                => TvShowItems(),

            ChannelItemFactory.CloudF1FolderId
                when _channelKind == MediaVaultChannelKind.Formula1
                => F1SeasonItems(),

            _ => ChildItems(folderId)
        };

        var totalCount = items.Count;

        var startIndex = Math.Max(
            query?.StartIndex.GetValueOrDefault() ?? 0,
            0);

        var limit = query?.Limit.GetValueOrDefault() ?? 0;

        if (startIndex >= totalCount)
        {
            return Task.FromResult(new ChannelItemResult
            {
                Items = [],
                TotalRecordCount = totalCount
            });
        }

        if (limit <= 0)
        {
            limit = totalCount - startIndex;
        }

        return Task.FromResult(new ChannelItemResult
        {
            Items = items
                .Skip(startIndex)
                .Take(limit)
                .ToList(),

            TotalRecordCount = totalCount
        });
    }

    private List<ChannelItemInfo> RootItems() => _channelKind switch
    {
        MediaVaultChannelKind.Movies => MovieItems(),
        MediaVaultChannelKind.TvShows => TvShowItems(),
        MediaVaultChannelKind.Formula1 => F1SeasonItems(),
        _ => []
    };

    private List<ChannelItemInfo> MovieItems() =>
        _catalogueStore.Movies
            .OrderBy(
                movie => movie.SortTitle ?? movie.Title,
                StringComparer.OrdinalIgnoreCase)
            .Select(ChannelItemFactory.Movie)
            .ToList();

    private List<ChannelItemInfo> TvShowItems() =>
        _catalogueStore.TvShows
            .OrderBy(
                show => show.SortTitle ?? show.Title,
                StringComparer.OrdinalIgnoreCase)
            .Select(ChannelItemFactory.TvShow)
            .ToList();

    private List<ChannelItemInfo> F1SeasonItems() =>
        _catalogueStore.F1Seasons
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Year)
            .Select(ChannelItemFactory.F1Season)
            .ToList();

    private List<ChannelItemInfo> ChildItems(string folderId)
    {
        if (folderId.StartsWith(
                "tv-show:",
                StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(folderId[8..], out var showId))
        {
            var show = _catalogueStore.TvShows
                .FirstOrDefault(x => x.Id == showId);

            return show?.Seasons
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.SeasonNumber)
                .Select(x => ChannelItemFactory.TvSeason(show, x))
                .ToList() ?? [];
        }

        if (folderId.StartsWith(
                "tv-season:",
                StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(folderId[10..], out var seasonId))
        {
            foreach (var show in _catalogueStore.TvShows)
            {
                var season = show.Seasons
                    .FirstOrDefault(x => x.Id == seasonId);

                if (season is not null)
                {
                    return season.Episodes
                        .OrderBy(x => x.DisplayOrder)
                        .ThenBy(
                            x => x.DisplayEpisodeNumber
                                ?? x.EpisodeNumber
                                ?? int.MaxValue)
                        .Select(
                            x => ChannelItemFactory.TvEpisode(
                                show,
                                season,
                                x))
                        .ToList();
                }
            }

            return [];
        }

        if (folderId.StartsWith(
                "f1-season:",
                StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(folderId[10..], out var f1SeasonId))
        {
            var season = _catalogueStore.F1Seasons
                .FirstOrDefault(x => x.Id == f1SeasonId);

            return season?.Rounds
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.RoundNumber)
                .Select(x => ChannelItemFactory.F1Round(season, x))
                .ToList() ?? [];
        }

        if (folderId.StartsWith(
                "f1-round:",
                StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(folderId[9..], out var roundId))
        {
            foreach (var season in _catalogueStore.F1Seasons)
            {
                var round = season.Rounds
                    .FirstOrDefault(x => x.Id == roundId);

                if (round is not null)
                {
                    return round.Events
                        .OrderBy(x => F1EventOrder(x.EventType))
                        .ThenBy(x => x.DisplayOrder)
                        .Select(
                            x => ChannelItemFactory.F1Event(
                                season,
                                round,
                                x))
                        .ToList();
                }
            }
        }

        _logger.LogDebug(
            "Unknown Media Vault channel folder ID: {FolderId}",
            folderId);

        return [];
    }

    private static int F1EventOrder(string eventType) =>
        eventType.Trim() switch
        {
            "Sprint Qualifying" => 0,
            "Sprint" => 1,
            "Qualifying" => 2,
            "Race" => 3,
            _ => 100
        };

    /// <summary>
    /// Resolves playback for a Media Vault catalogue item.
    ///
    /// The channel itself does not become the media provider.
    /// Jellyfin remains responsible for the actual local file and playback
    /// pipeline.
    /// </summary>
    public Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(
        string id,
        CancellationToken cancellationToken)
    {
        if (FindCatalogueMovie(id) is not null)
        {
            return GetMovieMediaInfoAsync(
                id,
                cancellationToken);
        }

        if (FindCatalogueTvEpisode(id) is not null)
        {
            return GetTvEpisodeMediaInfoAsync(
                id,
                cancellationToken);
        }

        if (FindCatalogueF1Event(id) is not null)
        {
            return GetF1EventMediaInfoAsync(
                id,
                cancellationToken);
        }

        _logger.LogWarning(
            "Media Vault channel item {Id} was not found in the catalogue.",
            id);

        return Task.FromResult<IEnumerable<MediaSourceInfo>>([]);
    }

    private async Task<IEnumerable<MediaSourceInfo>> GetMovieMediaInfoAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var performanceStopwatch = Stopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "[Media Vault PERF] Movie playback START: CatalogueId={CatalogueId}.",
            id);

        var movie = FindCatalogueMovie(id);

        if (movie is null)
        {
            _logger.LogWarning(
                "Media Vault movie {Id} was not found in the catalogue.",
                id);

            return [];
        }

        if (string.IsNullOrWhiteSpace(movie.GooglePhotosLink))
        {
            _logger.LogWarning(
                "Media Vault movie {Title} has no Google Photos share link.",
                movie.Title);

            return [];
        }

        var config = Plugin.Instance?.Configuration;

        if (config is null
            || string.IsNullOrWhiteSpace(
                config.MoviesDownloadDirectory))
        {
            _logger.LogWarning(
                "Cannot resolve {Title}: Movies download directory is not configured.",
                movie.Title);

            return [];
        }

        var directory = Path.GetFullPath(
            config.MoviesDownloadDirectory);

        Directory.CreateDirectory(directory);

        var requestedFilename = movie.Title + MovieExtension;

        var expectedFallbackPath = Path.Combine(
            directory,
            requestedFilename);

        // ------------------------------------------------------------
        // 1. Cached local file.
        // ------------------------------------------------------------

        if (File.Exists(expectedFallbackPath))
        {
            _logger.LogInformation(
                "Media Vault movie {Title} is already cached locally at {Path}. " +
                "No GPhotosDL download will be started. " +
                "Attempting targeted Jellyfin discovery.",
                movie.Title,
                expectedFallbackPath);

            var cachedLibraryItem =
                await EnsureTargetedLibraryItemAsync(
                    expectedFallbackPath,
                    movie.Title,
                    config,
                    cancellationToken,
                    movie.SortTitle)
                .ConfigureAwait(false);

            if (cachedLibraryItem is not null)
            {
        var CachedWaitForNormalLibraryMediaSourceAsyncStopwatch = Stopwatch.StartNew();
                var cachedMediaSource =
                    await WaitForNormalLibraryMediaSourceAsync(
                        cachedLibraryItem,
                        movie.Title,
                        config,
                        cancellationToken)
                    .ConfigureAwait(false);

        _logger.LogInformation(
            "[Media Vault PERF] Cached WaitForNormalLibraryMediaSourceAsync completed in {ElapsedMs} ms.",
            CachedWaitForNormalLibraryMediaSourceAsyncStopwatch.Elapsed.TotalMilliseconds);

                if (cachedMediaSource is not null)
                {
                    LogResolvedMediaSource(
                        movie.Title,
                        cachedMediaSource,
                        "cached local movie");

                    _logger.LogInformation(
                        "[Media Vault PERF] Cached movie playback TOTAL: {ElapsedMs} ms for {Title}.",
                        performanceStopwatch.Elapsed.TotalMilliseconds,
                        movie.Title);

                    return BuildClientCompatibleMediaSources(
                        cachedMediaSource,
                        movie.Title,
                        id);
                }

                _logger.LogWarning(
                    "Cached movie {Title} was found as Jellyfin library item " +
                    "{ItemId}, but Jellyfin has not produced a usable normal " +
                    "media source yet.",
                    movie.Title,
                    cachedLibraryItem.Id);
            }

            _logger.LogInformation(
                "Cached file for {Title} exists locally but is not currently " +
                "available as a usable Jellyfin library source.",
                movie.Title);
        }

        // ------------------------------------------------------------
        // 2. Start/reuse GPhotosDL.
        // ------------------------------------------------------------

        GPhotosDlService.DownloadStartResult? started;

        try
        {
        var GPhotosDLStartAsyncStopwatch = Stopwatch.StartNew();
            started = await _gphotosDl.StartAsync(
                movie.MediaVaultId,
                movie.GooglePhotosLink,
                requestedFilename,
                directory,
                config.RequestTimeoutSeconds,
                cancellationToken)
                .ConfigureAwait(false);

        _logger.LogInformation(
            "[Media Vault PERF] GPhotosDL StartAsync completed in {ElapsedMs} ms.",
            GPhotosDLStartAsyncStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start GPhotosDL download for {Title}.",
                movie.Title);

            return [];
        }

        if (started is null)
        {
            _logger.LogWarning(
                "GPhotosDL did not return a download job for {Title}.",
                movie.Title);

            return [];
        }

        var targetPath = _gphotosDl.GetExpectedPath(
            directory,
            started.Filename);

        _logger.LogInformation(
            "GPhotosDL job {DownloadId} started/reused for {Title}. " +
            "Expected local path: {Path}.",
            started.Id,
            movie.Title,
            targetPath);

        // ------------------------------------------------------------
        // 3. Wait until the file is probeable.
        // ------------------------------------------------------------

        var WaitForLocalFileReadyAsyncStopwatch = Stopwatch.StartNew();
        var fileReady = await WaitForLocalFileReadyAsync(
            targetPath,
            movie,
            started,
            config,
            cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[Media Vault PERF] WaitForLocalFileReadyAsync completed in {ElapsedMs} ms.",
            WaitForLocalFileReadyAsyncStopwatch.Elapsed.TotalMilliseconds);

        if (!fileReady)
        {
            _logger.LogWarning(
                "The local file for {Title} did not become readable within " +
                "the configured startup/discovery timeout. Path: {Path}",
                movie.Title,
                targetPath);

            return [];
        }

        _logger.LogInformation(
            "Local file for {Title} is available and readable at {Path}. " +
            "The file may still be actively downloading. " +
            "Beginning targeted Jellyfin discovery for this exact file.",
            movie.Title,
            targetPath);

        // ------------------------------------------------------------
        // 4. TARGETED Jellyfin discovery.
        //
        // IMPORTANT:
        //
        // There is deliberately NO QueueLibraryScan() here.
        // ------------------------------------------------------------

        var EnsureTargetedLibraryItemAsyncStopwatch = Stopwatch.StartNew();
        var libraryItem =
            await EnsureTargetedLibraryItemAsync(
                targetPath,
                movie.Title,
                config,
                cancellationToken,
                movie.SortTitle)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[Media Vault PERF] EnsureTargetedLibraryItemAsync completed in {ElapsedMs} ms.",
            EnsureTargetedLibraryItemAsyncStopwatch.Elapsed.TotalMilliseconds);

        if (libraryItem is null)
        {
            _logger.LogWarning(
                "Jellyfin could not resolve/register the local movie {Title} " +
                "at {Path} using targeted item discovery.",
                movie.Title,
                targetPath);

            return [];
        }

        // ------------------------------------------------------------
        // 5. Wait for the REAL Jellyfin media source.
        // ------------------------------------------------------------

        var WaitForNormalLibraryMediaSourceAsyncStopwatch = Stopwatch.StartNew();
        var mediaSource =
            await WaitForNormalLibraryMediaSourceAsync(
                libraryItem,
                movie.Title,
                config,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[Media Vault PERF] WaitForNormalLibraryMediaSourceAsync completed in {ElapsedMs} ms.",
            WaitForNormalLibraryMediaSourceAsyncStopwatch.Elapsed.TotalMilliseconds);

        if (mediaSource is null)
        {
            _logger.LogWarning(
                "Jellyfin discovered {Title} at {Path}, but did not produce " +
                "a usable normal library media source within the configured " +
                "timeout.",
                movie.Title,
                targetPath);

            return [];
        }

        LogResolvedMediaSource(
            movie.Title,
            mediaSource,
            "targeted normal Jellyfin Movies library");

        _logger.LogInformation(
            "[Media Vault PERF] Movie playback TOTAL: {ElapsedMs} ms for {Title}.",
            performanceStopwatch.Elapsed.TotalMilliseconds,
            movie.Title);

        return BuildClientCompatibleMediaSources(
            mediaSource,
            movie.Title,
            id);
    }

    private CloudMovieDto? FindCatalogueMovie(string id)
    {
        return _catalogueStore.Movies.FirstOrDefault(
            x => string.Equals(
                x.Id.ToString(
                    "N",
                    CultureInfo.InvariantCulture),
                id,
                StringComparison.OrdinalIgnoreCase));
    }

    private CloudTvEpisodeDto? FindCatalogueTvEpisode(string id)
    {
        return _catalogueStore.TvShows
            .SelectMany(
                show => show.Seasons
                    .SelectMany(season => season.Episodes))
            .FirstOrDefault(
                x => string.Equals(
                    x.Id.ToString(
                        "N",
                        CultureInfo.InvariantCulture),
                    id,
                    StringComparison.OrdinalIgnoreCase));
    }

    private (
        CloudTvShowDto Show,
        CloudTvSeasonDto Season,
        CloudTvEpisodeDto Episode)?
        FindCatalogueTvEpisodeContext(string id)
    {
        foreach (var show in _catalogueStore.TvShows)
        {
            foreach (var season in show.Seasons)
            {
                var episode = season.Episodes.FirstOrDefault(
                    x => string.Equals(
                        x.Id.ToString(
                            "N",
                            CultureInfo.InvariantCulture),
                        id,
                        StringComparison.OrdinalIgnoreCase));

                if (episode is not null)
                {
                    return (
                        show,
                        season,
                        episode);
                }
            }
        }

        return null;
    }

    private (
        CloudF1SeasonDto Season,
        CloudF1RoundDto Round,
        CloudF1EventDto Event)?
        FindCatalogueF1Event(string id)
    {
        foreach (var season in _catalogueStore.F1Seasons)
        {
            foreach (var round in season.Rounds)
            {
                var @event = round.Events.FirstOrDefault(
                    x =>
                        string.Equals(
                            ChannelItemFactory.F1EventItemId(x.Id),
                            id,
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        string.Equals(
                            x.Id.ToString(
                                "N",
                                CultureInfo.InvariantCulture),
                            id,
                            StringComparison.OrdinalIgnoreCase));

                if (@event is not null)
                {
                    return (
                        season,
                        round,
                        @event);
                }
            }
        }

        return null;
    }

    private async Task<IEnumerable<MediaSourceInfo>>
        GetTvEpisodeMediaInfoAsync(
            string id,
            CancellationToken cancellationToken)
    {
        var context =
            FindCatalogueTvEpisodeContext(id);

        if (context is null)
        {
            return [];
        }

        var (
            show,
            season,
            episode) = context.Value;

        return await GetCloudVideoMediaInfoAsync(
                id,
                episode.MediaVaultId,
                episode.Title,
                episode.GooglePhotosLink,
                BuildTvEpisodeFilename(
                    show,
                    season,
                    episode),
                BuildTvEpisodeDirectory(
                    show,
                    season),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IEnumerable<MediaSourceInfo>>
        GetF1EventMediaInfoAsync(
            string id,
            CancellationToken cancellationToken)
    {
        var context =
            FindCatalogueF1Event(id);

        if (context is null)
        {
            return [];
        }

        var (
            season,
            round,
            @event) = context.Value;

        var filename =
            SafePathPart(@event.EventType) + MovieExtension;

        return await GetCloudVideoMediaInfoAsync(
                id,
                @event.MediaVaultId,
                @event.Title,
                @event.GooglePhotosLink,
                filename,
                BuildF1EventDirectory(
                    season,
                    round),
                cancellationToken)
            .ConfigureAwait(false);

    }

    private async Task<IEnumerable<MediaSourceInfo>>
        GetCloudVideoMediaInfoAsync(
            string requestedItemId,
            string mediaVaultId,
            string title,
            string? googlePhotosLink,
            string filename,
            string directory,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(googlePhotosLink))
        {
            _logger.LogWarning(
                "Media Vault item {Title} ({MediaVaultId}) has no Google Photos share link.",
                title,
                mediaVaultId);

            return [];
        }

        var config = Plugin.Instance?.Configuration;

        if (config is null || string.IsNullOrWhiteSpace(directory))
        {
            _logger.LogWarning(
                "Cannot resolve {Title}: the relevant download directory is not configured.",
                title);

            return [];
        }

        directory = Path.GetFullPath(directory);

        Directory.CreateDirectory(directory);

        var expectedPath = Path.Combine(
            directory,
            filename);

        // ------------------------------------------------------------
        // Cached TV/F1 file.
        // ------------------------------------------------------------

        if (File.Exists(expectedPath))
        {
            _logger.LogInformation(
                "Media Vault item {Title} is already cached at {Path}. " +
                "Using targeted Jellyfin discovery.",
                title,
                expectedPath);

            var cachedItem =
                await EnsureTargetedLibraryItemAsync(
                    expectedPath,
                    title,
                    config,
                    cancellationToken)
                .ConfigureAwait(false);

            if (cachedItem is not null)
            {
                var cachedSource =
                    await WaitForNormalLibraryMediaSourceAsync(
                        cachedItem,
                        title,
                        config,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (cachedSource is not null)
                {
                    LogResolvedMediaSource(
                        title,
                        cachedSource,
                        "cached local TV/F1 item");

                    return BuildClientCompatibleMediaSources(
                        cachedSource,
                        title,
                        requestedItemId);
                }
            }
        }

        // ------------------------------------------------------------
        // Start/reuse GPhotosDL.
        // ------------------------------------------------------------

        GPhotosDlService.DownloadStartResult? started;

        try
        {
            started = await _gphotosDl.StartAsync(
                    mediaVaultId,
                    googlePhotosLink,
                    filename,
                    directory,
                    config.RequestTimeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start GPhotosDL download for {Title}.",
                title);

            return [];
        }

        if (started is null)
        {
            return [];
        }

        var targetPath =
            _gphotosDl.GetExpectedPath(
                directory,
                started.Filename);

        _logger.LogInformation(
            "GPhotosDL job {DownloadId} started/reused for {Title}. " +
            "Expected local path: {Path}.",
            started.Id,
            title,
            targetPath);

        // ------------------------------------------------------------
        // Wait until enough data exists for Jellyfin.
        // ------------------------------------------------------------

        var fileReady =
            await WaitForLocalFileReadyAsync(
                targetPath,
                title,
                mediaVaultId,
                started,
                config,
                cancellationToken)
            .ConfigureAwait(false);

        if (!fileReady)
        {
            _logger.LogWarning(
                "The local file for {Title} did not become readable within " +
                "the configured startup timeout.",
                title);

            return [];
        }

        // ------------------------------------------------------------
        // TARGETED Jellyfin discovery.
        // ------------------------------------------------------------

        var libraryItem =
            await EnsureTargetedLibraryItemAsync(
                targetPath,
                title,
                config,
                cancellationToken)
            .ConfigureAwait(false);

        if (libraryItem is null)
        {
            _logger.LogWarning(
                "Jellyfin could not resolve/register {Title} at {Path} " +
                "using targeted discovery.",
                title,
                targetPath);

            return [];
        }

        var mediaSource =
            await WaitForNormalLibraryMediaSourceAsync(
                libraryItem,
                title,
                config,
                cancellationToken)
            .ConfigureAwait(false);

        if (mediaSource is null)
        {
            _logger.LogWarning(
                "Jellyfin discovered {Title} at {Path}, but did not produce " +
                "a usable media source within the configured timeout.",
                title,
                targetPath);

            return [];
        }

        LogResolvedMediaSource(
            title,
            mediaSource,
            "targeted normal Jellyfin library for TV/F1 item");

        return BuildClientCompatibleMediaSources(
            mediaSource,
            title,
            requestedItemId);
    }

    /// <summary>
    /// Builds the TV episode directory.
    ///
    /// The directory structure remains:
    ///
    /// TV root/
    ///   Show/
    ///     Season/
    ///
    /// The filename itself is handled separately.
    /// </summary>
    private static string BuildTvEpisodeDirectory(
        CloudTvShowDto show,
        CloudTvSeasonDto season)
    {
        var root =
            Plugin.Instance?.Configuration
                .TvShowsDownloadDirectory
            ?? string.Empty;

        return Path.Combine(
            root,
            SafePathPart(show.Title),
            SafePathPart(season.DisplayName));
    }

    private static string BuildTvEpisodeFilename(
        CloudTvShowDto show,
        CloudTvSeasonDto season,
        CloudTvEpisodeDto episode)
    {
        var number =
            episode.DisplayEpisodeNumber
            ?? episode.EpisodeNumber
            ?? (episode.DisplayOrder + 1);

        return $"{number}. {SafePathPart(episode.Title)}.mkv";
    }

    /// <summary>
    /// Builds the F1 event directory consistently from the actual round
    /// identity used by the catalogue.
    ///
    /// This avoids the previous situation where the directory could be based
    /// on a different round label from the one used to display the event.
    /// </summary>
    private static string BuildF1EventDirectory(
        CloudF1SeasonDto season,
        CloudF1RoundDto round)
    {
        var root =
            Plugin.Instance?.Configuration
                .F1DownloadDirectory
            ?? string.Empty;

        var roundName = FirstNonEmpty(
            round.GrandPrixName,
            round.Name,
            $"Round {round.RoundNumber}");

        return Path.Combine(
            root,
            SafePathPart(season.DisplayName),
            SafePathPart(roundName));
    }

    private static string SafePathPart(string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var chars =
            value
                .Trim()
                .Select(
                    c => invalid.Contains(c)
                        ? '_'
                        : c)
                .ToArray();

        var result =
            new string(chars)
                .Trim()
                .TrimEnd('.');

        return string.IsNullOrWhiteSpace(result)
            ? "Unknown"
            : result;
    }

    private static string FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            x => !string.IsNullOrWhiteSpace(x))
        ?.Trim()
        ?? string.Empty;

    // ========================================================================
    // TARGETED JELLYFIN DISCOVERY
    // ========================================================================

    /// <summary>
    /// Ensures that Jellyfin has a real BaseItem for exactly one filesystem
    /// path.
    ///
    /// This deliberately does NOT call ILibraryManager.QueueLibraryScan().
    ///
    /// ResolvePath() uses Jellyfin's normal item resolver pipeline but operates
    /// on the supplied FileSystemMetadata object. For a file, Jellyfin does not
    /// need to enumerate the entire containing directory just to resolve it.
    ///
    /// If the item already exists in Jellyfin, the existing database item is
    /// returned.
    ///
    /// If it does not exist, Jellyfin's resolved BaseItem is registered with
    /// the normal library repository.
    /// </summary>
    private async Task<BaseItem?> EnsureTargetedLibraryItemAsync(
        string targetPath,
        string title,
        PluginConfiguration config,
        CancellationToken cancellationToken,
        string? sortTitle = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullTargetPath = Path.GetFullPath(targetPath);

        // ------------------------------------------------------------
        // 1. Fast path: Jellyfin already knows this exact path.
        // ------------------------------------------------------------
        var existing = TryFindLibraryItem(fullTargetPath);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Jellyfin already has normal library item {ItemId} for " +
                "{Title} at {Path}. No discovery scan or metadata refresh " +
                "is required.",
                existing.Id,
                title,
                fullTargetPath);

            await ApplyCatalogueSortTitleAsync(
                    existing,
                    sortTitle,
                    title,
                    cancellationToken)
                .ConfigureAwait(false);

            return existing;
        }

        try
        {
            var fileInfo = new FileInfo(fullTargetPath);
            if (!fileInfo.Exists)
            {
                _logger.LogDebug(
                    "Targeted Jellyfin discovery skipped because {Path} " +
                    "does not currently exist.",
                    fullTargetPath);

                return null;
            }

            var jellyfinFileInfo = new FileSystemMetadata
            {
                FullName = fullTargetPath,
                Name = fileInfo.Name,
                Extension = fileInfo.Extension,
                Length = fileInfo.Length,
                Exists = fileInfo.Exists,
                IsDirectory = false,
                CreationTimeUtc = fileInfo.CreationTimeUtc,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            };

            // ------------------------------------------------------------
            // 2. Find the nearest already-registered parent folder.
            //
            // This uses exact DB path lookups only. It does NOT enumerate the
            // directory, validate children, or trigger a library scan.
            // ------------------------------------------------------------
            var parent = TryFindNearestParentFolder(fullTargetPath);

            _logger.LogInformation(
                "Resolving exact local Jellyfin item for {Title} at {Path} " +
                "using parent {ParentPath}.",
                title,
                fullTargetPath,
                parent?.Path ?? "<none>");

            // ------------------------------------------------------------
            // 3. Ask Jellyfin to resolve THIS FILE only.
            // ------------------------------------------------------------
            var resolvedItem = _libraryManager.ResolvePath(
                jellyfinFileInfo,
                parent,
                null);

            if (resolvedItem is null)
            {
                _logger.LogWarning(
                    "Jellyfin's normal resolver returned no BaseItem for " +
                    "{Title} at {Path}.",
                    title,
                    fullTargetPath);

                return null;
            }

            _logger.LogInformation(
                "Jellyfin resolved {Title} at {Path} as {ItemType} " +
                "with BaseItem ID {ItemId}.",
                title,
                fullTargetPath,
                resolvedItem.GetType().Name,
                resolvedItem.Id);

            // ------------------------------------------------------------
            // 4. The filesystem watcher can race us. Check both the resolved
            // ID and the exact path before attempting to create a duplicate.
            // ------------------------------------------------------------
            var persisted = _libraryManager.GetItemById(resolvedItem.Id)
                ?? TryFindLibraryItem(fullTargetPath);

            if (persisted is not null)
            {
                _logger.LogInformation(
                    "Resolved Jellyfin item {ItemId} for {Title} is already " +
                    "registered in the library. No metadata refresh is " +
                    "required.",
                    persisted.Id,
                    title);

                await ApplyCatalogueSortTitleAsync(
                        persisted,
                        sortTitle,
                        title,
                        cancellationToken)
                    .ConfigureAwait(false);

                return persisted;
            }

            // ------------------------------------------------------------
            // 5. Register the exact resolved BaseItem under the nearest known
            // parent. This is NOT a library-wide scan.
            // ------------------------------------------------------------
            _logger.LogInformation(
                "Registering newly resolved Jellyfin BaseItem {ItemId} " +
                "for {Title} at {Path} without a library-wide scan.",
                resolvedItem.Id,
                title,
                fullTargetPath);

            try
            {
                _libraryManager.CreateItem(
                    resolvedItem,
                    parent);
            }
            catch (Exception createException)
            {
                // Jellyfin's normal filesystem watcher may have registered the
                // file between our checks. Treat that as a benign race if the
                // exact path is now present in the database.
                var racedItem = TryFindLibraryItem(fullTargetPath);
                if (racedItem is null)
                {
                    throw;
                }

                _logger.LogInformation(
                    createException,
                    "Jellyfin registered {Title} concurrently while targeted " +
                    "discovery was creating it. Using the existing item {ItemId}.",
                    title,
                    racedItem.Id);

                await ApplyCatalogueSortTitleAsync(
                        racedItem,
                        sortTitle,
                        title,
                        cancellationToken)
                    .ConfigureAwait(false);

                return racedItem;
            }

            // ------------------------------------------------------------
            // 6. Retrieve the actual persisted BaseItem.
            // ------------------------------------------------------------
            var registered = _libraryManager.GetItemById(resolvedItem.Id)
                ?? TryFindLibraryItem(fullTargetPath);

            if (registered is null)
            {
                _logger.LogWarning(
                    "Jellyfin resolved and attempted to register {Title}, " +
                    "but the persisted BaseItem could not be retrieved.",
                    title);

                return null;
            }

            _logger.LogInformation(
                "Jellyfin registered {Title} as normal library item {ItemId} " +
                "at {Path}.",
                title,
                registered.Id,
                registered.Path);

            // ------------------------------------------------------------
            // 7. Apply the catalogue sort title to the actual Jellyfin item.
            //
            // IMPORTANT:
            //
            // Do NOT call BaseItem.RefreshMetadata() here.
            //
            // RefreshMetadata() enters Jellyfin's metadata/probe pipeline,
            // which on the NAS can trigger expensive FFmpeg image extraction
            // and Screen Grabber work before playback can begin.
            //
            // The actual BaseItem has already been resolved and registered
            // above. Playback will ask Jellyfin for the real media source
            // directly afterwards.
            // ------------------------------------------------------------
            await ApplyCatalogueSortTitleAsync(
                    registered,
                    sortTitle,
                    title,
                    cancellationToken)
                .ConfigureAwait(false);

            return registered;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Targeted Jellyfin discovery failed for {Title} at {Path}.",
                title,
                fullTargetPath);

            return null;
        }
    }

    /// <summary>
    /// Finds the nearest already-registered Jellyfin folder above the target.
    ///
    /// This is deliberately implemented as exact database path lookups only.
    /// It does not enumerate any directory contents and therefore does not turn
    /// targeted playback back into a library scan.
    /// </summary>
    private Folder? TryFindNearestParentFolder(string fullTargetPath)
    {
        try
        {
            var parentPath = Path.GetDirectoryName(fullTargetPath);

            while (!string.IsNullOrWhiteSpace(parentPath))
            {
                var parent = _libraryManager.FindByPath(parentPath, true) as Folder;
                if (parent is not null)
                {
                    return parent;
                }

                var nextParent = Path.GetDirectoryName(
                    parentPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

                if (string.Equals(
                        nextParent,
                        parentPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                parentPath = nextParent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Unable to find a registered Jellyfin parent folder for {Path}.",
                fullTargetPath);
        }

        return null;
    }

    /// <summary>
    /// Applies the catalogue sort title to the actual Jellyfin library item.
    /// ForcedSortName is used deliberately: Jellyfin's SortName derives from
    /// this forced value, allowing the catalogue's sort title to survive normal
    /// metadata refreshes.
    /// </summary>
    private async Task ApplyCatalogueSortTitleAsync(
        BaseItem libraryItem,
        string? sortTitle,
        string title,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Media Vault ApplyCatalogueSortTitleAsync CALLED: Title={Title}, SortTitle={SortTitle}, ItemId={ItemId}",
            title,
            sortTitle,
            libraryItem.Id);

        if (string.IsNullOrWhiteSpace(sortTitle))
        {
            return;
        }

        var desiredSortTitle = sortTitle.Trim();

        if (string.Equals(
                libraryItem.ForcedSortName,
                desiredSortTitle,
                StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Media Vault sort-title ALREADY SET: Title={Title}, ForcedSortName={ForcedSortName}, SortName={SortName}, DesiredSortTitle={DesiredSortTitle}, ItemId={ItemId}",
                libraryItem.Name,
                libraryItem.ForcedSortName,
                libraryItem.SortName,
                desiredSortTitle,
                libraryItem.Id);

            var persistedItem = _libraryManager.GetItemById(libraryItem.Id);

            _logger.LogInformation(
                "Media Vault sort-title PERSISTED CHECK: Title={Title}, ForcedSortName={ForcedSortName}, SortName={SortName}",
                persistedItem?.Name,
                persistedItem?.ForcedSortName,
                persistedItem?.SortName);

            return;
        }


        _logger.LogInformation(
            "Media Vault sort-title BEFORE apply: Title={Title}, ForcedSortName={ForcedSortName}, SortName={SortName}, DesiredSortTitle={DesiredSortTitle}",
            libraryItem.Name,
            libraryItem.ForcedSortName,
            libraryItem.SortName,
            desiredSortTitle);


        libraryItem.ForcedSortName = desiredSortTitle;

        try
        {
            await libraryItem
                .UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Applied catalogue sort title {SortTitle} to Jellyfin item " +
                "{ItemId} for {Title}.",
                desiredSortTitle,
                libraryItem.Id,
                title);

            _logger.LogInformation(
                "Media Vault sort-title AFTER apply: Title={Title}, ForcedSortName={ForcedSortName}, SortName={SortName}",
                libraryItem.Name,
                libraryItem.ForcedSortName,
                libraryItem.SortName);

            var persistedItem = _libraryManager.GetItemById(libraryItem.Id);

            _logger.LogInformation(
                "Media Vault sort-title PERSISTED CHECK: Title={Title}, ForcedSortName={ForcedSortName}, SortName={SortName}",
                persistedItem?.Name,
                persistedItem?.ForcedSortName,
                persistedItem?.SortName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to persist catalogue sort title {SortTitle} to " +
                "Jellyfin item {ItemId} for {Title}.",
                desiredSortTitle,
                libraryItem.Id,
                title);
        }
    }

    /// <summary>
    /// Waits for the normal Jellyfin library item to exist.
    ///
    /// This method is retained for compatibility with the existing playback
    /// architecture, but it no longer starts library-wide scans itself.
    /// </summary>
    private async Task<BaseItem?> WaitForLibraryItemAsync(
        string targetPath,
        string movieTitle,
        PluginConfiguration config,
        bool queueScanIfMissing,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds =
            GetLibraryDiscoveryWaitSeconds(config);

        var deadline =
            DateTime.UtcNow.AddSeconds(timeoutSeconds);

        var loggedWaitingMessage = false;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item =
                TryFindLibraryItem(targetPath);

            if (item is not null)
            {
                return item;
            }

            if (!loggedWaitingMessage)
            {
                _logger.LogInformation(
                    "Waiting for Jellyfin to discover {Title} as a normal " +
                    "library item at {Path}.",
                    movieTitle,
                    targetPath);

                loggedWaitingMessage = true;
            }

            // This argument is retained for compatibility with existing
            // callers, but intentionally does nothing.
            //
            // QueueLibraryScan() was the source of the expensive whole-library
            // scan behaviour we are eliminating.
            if (queueScanIfMissing)
            {
                _logger.LogInformation(
                    "Jellyfin does not currently have a library item for " +
                    "{Title}; targeted discovery is required instead of a " +
                    "whole-library scan.",
                    movieTitle);

                queueScanIfMissing = false;
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        LibraryDiscoveryPollMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return TryFindLibraryItem(targetPath);
    }

    /// <summary>
    /// Waits for Jellyfin's normal media-source pipeline to produce a usable
    /// source for the discovered library item.
    /// </summary>
    private async Task<MediaSourceInfo?>
        WaitForNormalLibraryMediaSourceAsync(
            BaseItem libraryItem,
            string movieTitle,
            PluginConfiguration config,
            CancellationToken cancellationToken)
    {
        var timeoutSeconds =
            GetLibraryDiscoveryWaitSeconds(config);

        var deadline =
            DateTime.UtcNow.AddSeconds(timeoutSeconds);

        // First, try the cheap path. If the real Jellyfin media source is
        // already persisted, there is no reason to probe anything.
        var existingMediaSource =
            await TryGetNormalLibraryMediaSourceAsync(
                    libraryItem,
                    movieTitle,
                    cancellationToken)
                .ConfigureAwait(false);

        if (IsUsableLocalMediaSource(existingMediaSource))
        {
            _logger.LogInformation(
                "Jellyfin's normal media source for {Title} was already " +
                "available; targeted media probing was not required.",
                movieTitle);

            return existingMediaSource;
        }

        // The item exists, but Jellyfin has not persisted the media-stream
        // information required to construct a usable normal MediaSourceInfo.
        // Probe this exact file only. This deliberately does NOT call
        // BaseItem.RefreshMetadata() or MediaSourceManager with probing
        // enabled, because those paths can enter Jellyfin's full metadata
        // pipeline and undo the NAS performance work.
        await ProbeTargetedLibraryItemAsync(
                libraryItem,
                movieTitle,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);

        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            attempt++;

            var mediaSource =
                await TryGetNormalLibraryMediaSourceAsync(
                        libraryItem,
                        movieTitle,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (IsUsableLocalMediaSource(mediaSource))
            {
                _logger.LogInformation(
                    "Jellyfin's normal media source for {Title} became " +
                    "available after {Attempt} post-probe attempt(s).",
                    movieTitle,
                    attempt);

                return mediaSource;
            }

            _logger.LogDebug(
                "Jellyfin media source for {Title} is still not ready " +
                "after targeted probing and {Attempt} attempt(s).",
                movieTitle,
                attempt);

            await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        MediaSourcePollMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogWarning(
            "Timed out waiting for Jellyfin's normal media source for {Title} " +
            "after targeted media probing.",
            movieTitle);

        return await TryGetNormalLibraryMediaSourceAsync(
                libraryItem,
                movieTitle,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Probes the exact local file represented by <paramref name="libraryItem"/>
    /// and persists only the playback-critical media information that Jellyfin
    /// needs to build its normal MediaSourceInfo.
    ///
    /// This is intentionally much narrower than BaseItem.RefreshMetadata():
    /// chapters are disabled, no metadata providers are invoked, and no
    /// images/I-frame extraction are requested.
    /// </summary>
    private async Task<bool>
        ProbeTargetedLibraryItemAsync(
            BaseItem libraryItem,
            string movieTitle,
            DateTime deadline,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(libraryItem.Path)
            || !File.Exists(libraryItem.Path))
        {
            _logger.LogWarning(
                "Cannot targeted-probe {Title}: the Jellyfin item path " +
                "does not currently exist: {Path}",
                movieTitle,
                libraryItem.Path);

            return false;
        }

        var probeAttempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            probeAttempt++;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "[Media Vault PERF] Targeted media probe START for {Title} " +
                    "attempt {Attempt}: {Path}",
                    movieTitle,
                    probeAttempt,
                    libraryItem.Path);

                var mediaInfo =
                    await _mediaEncoder.GetMediaInfo(
                            new MediaInfoRequest
                            {
                                MediaSource = new MediaSourceInfo
                                {
                                    Path = libraryItem.Path,
                                    Protocol = MediaProtocol.File,
                                },
                                MediaType = DlnaProfileType.Video,
                                ExtractChapters = false
                            },
                            cancellationToken)
                        .ConfigureAwait(false);

                if (mediaInfo.MediaStreams is null
                    || mediaInfo.MediaStreams.Count == 0)
                {
                    throw new InvalidOperationException(
                        "The targeted media probe returned no media streams.");
                }

                if (libraryItem is not Video videoItem)
                {
                    throw new InvalidOperationException(
                        $"The resolved Jellyfin item is not a Video: {libraryItem.GetType().Name}.");
                }

                var mediaStreams = mediaInfo.MediaStreams.ToList();
                var videoStream = mediaStreams.FirstOrDefault(
                    stream => stream.Type == MediaStreamType.Video);

                if (videoStream is null)
                {
                    throw new InvalidOperationException(
                        "The targeted media probe returned no video stream.");
                }

                // FFProbeVideoInfo assigns stable Jellyfin stream indexes before
                // persisting streams. Mirror that behaviour here because these
                // indexes become part of the normal MediaSourceInfo used later
                // by playback.
                for (var i = 0; i < mediaStreams.Count; i++)
                {
                    mediaStreams[i].Index = i;
                }

                _mediaStreamRepository.SaveMediaStreams(
                    libraryItem.Id,
                    mediaStreams,
                    cancellationToken);

                videoItem.Container = mediaInfo.Container;
                videoItem.TotalBitrate = mediaInfo.Bitrate;
                videoItem.RunTimeTicks = mediaInfo.RunTimeTicks;
                videoItem.Width = videoStream.Width ?? 0;
                videoItem.Height = videoStream.Height?? 0;
                videoItem.DefaultVideoStreamIndex = videoStream.Index;
                videoItem.HasSubtitles = mediaStreams.Any(
                    stream => stream.Type == MediaStreamType.Subtitle);

                await videoItem.UpdateToRepositoryAsync(
                        ItemUpdateType.MetadataImport,
                        cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();

                _logger.LogInformation(
                    "[Media Vault PERF] Targeted media probe SUCCESS for " +
                    "{Title} in {ElapsedMs} ms. Container={Container}, " +
                    "Streams={StreamCount}, Bitrate={Bitrate}, RuntimeTicks={RuntimeTicks}, " +
                    "Resolution={Width}x{Height}.",
                    movieTitle,
                    stopwatch.Elapsed.TotalMilliseconds,
                    mediaInfo.Container,
                    mediaStreams.Count,
                    mediaInfo.Bitrate,
                    mediaInfo.RunTimeTicks,
                    videoStream.Width,
                    videoStream.Height);

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogDebug(
                    ex,
                    "[Media Vault PERF] Targeted media probe attempt {Attempt} " +
                    "for {Title} failed after {ElapsedMs} ms. The file may " +
                    "still be downloading; will retry without triggering a " +
                    "library scan or full metadata refresh.",
                    probeAttempt,
                    movieTitle,
                    stopwatch.Elapsed.TotalMilliseconds);

                if (DateTime.UtcNow >= deadline)
                {
                    _logger.LogWarning(
                        "Targeted media probing ultimately failed for {Title} " +
                        "before the playback discovery deadline.",
                        movieTitle);

                    return false;
                }

                await Task.Delay(
                        TimeSpan.FromMilliseconds(500),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the REAL media source from the Jellyfin library item.
    /// </summary>
    private async Task<MediaSourceInfo?>
        TryGetNormalLibraryMediaSourceAsync(
            BaseItem libraryItem,
            string movieTitle,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var sources =
                libraryItem.GetMediaSources(false);

            if (sources is not null)
            {
                var source =
                    sources.FirstOrDefault(
                        IsUsableLocalMediaSource);

                if (source is not null)
                {
                    return source;
                }
            }

            var mediaSource =
                await _mediaSourceManager.GetMediaSource(
                        libraryItem,
                        null,
                        null,
                        true,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (IsUsableLocalMediaSource(mediaSource))
            {
                return mediaSource;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Jellyfin's normal media-source pipeline is not ready yet " +
                "for {Title} ({ItemId}).",
                movieTitle,
                libraryItem.Id);

            return null;
        }
    }

    private static bool IsUsableLocalMediaSource(
        MediaSourceInfo? mediaSource)
    {
        if (mediaSource is null)
        {
            return false;
        }

        if (mediaSource.Protocol != MediaProtocol.File)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                mediaSource.Path))
        {
            return false;
        }

        if (mediaSource.MediaStreams is null
            || !mediaSource.MediaStreams.Any())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Waits for a GPhotosDL-created file to become usable enough for Jellyfin
    /// to inspect it.
    ///
    /// This is intentionally NOT a download-completion check.
    /// </summary>
    private async Task<bool> WaitForLocalFileReadyAsync(
        string targetPath,
        string title,
        string mediaVaultId,
        GPhotosDlService.DownloadStartResult started,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds =
            GetLibraryDiscoveryWaitSeconds(config);

        var deadline =
            DateTime.UtcNow.AddSeconds(timeoutSeconds);

        var readableChecks = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(targetPath))
            {
                readableChecks = 0;

                var status =
                    await TryGetDownloadStatusAsync(
                            mediaVaultId,
                            config.RequestTimeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (status is not null
                    && status.Status is "failed" or "cancelled")
                {
                    _logger.LogWarning(
                        "GPhotosDL job {DownloadId} ended as {Status}: {Error}",
                        status.Id ?? started.Id,
                        status.Status,
                        status.Error);

                    return false;
                }

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            long length;

            try
            {
                var currentFileInfo = new FileInfo(targetPath);
                length = currentFileInfo.Length;
            }
            catch (IOException)
            {
                readableChecks = 0;

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }
            catch (UnauthorizedAccessException)
            {
                readableChecks = 0;

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            if (length < MinimumBytesBeforeLibraryInspection)
            {
                readableChecks = 0;

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            if (CanOpenFileForReading(targetPath))
            {
                readableChecks++;

                if (readableChecks >= RequiredReadableChecks)
                {
                    return true;
                }
            }
            else
            {
                readableChecks = 0;
            }

            await Task.Delay(
                    LibraryDiscoveryPollMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return File.Exists(targetPath)
            && CanOpenFileForReading(targetPath);
    }

    private async Task<bool> WaitForLocalFileReadyAsync(
        string targetPath,
        CloudMovieDto movie,
        GPhotosDlService.DownloadStartResult started,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds =
            GetLibraryDiscoveryWaitSeconds(config);

        var deadline =
            DateTime.UtcNow.AddSeconds(timeoutSeconds);

        var readableChecks = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(targetPath))
            {
                readableChecks = 0;

                var status =
                    await TryGetDownloadStatusAsync(
                            movie,
                            config.RequestTimeoutSeconds,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (status is not null
                    && status.Status is "failed" or "cancelled")
                {
                    _logger.LogWarning(
                        "GPhotosDL job {DownloadId} ended as {Status}: {Error}",
                        status.Id ?? started.Id,
                        status.Status,
                        status.Error);

                    return false;
                }

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            long length;

            try
            {
                length =
                    new FileInfo(targetPath).Length;
            }
            catch (IOException)
            {
                readableChecks = 0;

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }
            catch (UnauthorizedAccessException)
            {
                readableChecks = 0;

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            if (length < MinimumBytesBeforeLibraryInspection)
            {
                readableChecks = 0;

                _logger.LogDebug(
                    "GPhotosDL file for {Title} exists but is only {Size} bytes. " +
                    "Waiting for more initial data before asking Jellyfin to " +
                    "inspect it.",
                    movie.Title,
                    length);

                await Task.Delay(
                        LibraryDiscoveryPollMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

            if (CanOpenFileForReading(targetPath))
            {
                readableChecks++;

                _logger.LogDebug(
                    "GPhotosDL file for {Title} is readable ({Size} bytes). " +
                    "Readiness check {Check}/{Required}.",
                    movie.Title,
                    length,
                    readableChecks,
                    RequiredReadableChecks);

                if (readableChecks >= RequiredReadableChecks)
                {
                    return true;
                }
            }
            else
            {
                readableChecks = 0;

                _logger.LogDebug(
                    "The local file for {Title} exists ({Size} bytes) but " +
                    "Windows currently does not allow a read handle. " +
                    "Waiting before asking Jellyfin to inspect it.",
                    movie.Title,
                    length);
            }

            await Task.Delay(
                    LibraryDiscoveryPollMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return File.Exists(targetPath)
            && CanOpenFileForReading(targetPath);
    }

    private static bool CanOpenFileForReading(
        string path)
    {
        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

            return stream.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private BaseItem? TryFindLibraryItem(
        string targetPath)
    {
        try
        {
            var fullTargetPath =
                Path.GetFullPath(targetPath);

            var item =
                _libraryManager.FindByPath(
                    fullTargetPath,
                    false);

            if (item is not null)
            {
                return item;
            }

            var candidates =
                _libraryManager.GetItemList(
                    new InternalItemsQuery
                    {
                        Path = fullTargetPath,
                        IsFolder = false,
                        Limit = 10
                    });

            return candidates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Unable to look up Jellyfin library item for {Path}.",
                targetPath);

            return null;
        }
    }

    private static int GetLibraryDiscoveryWaitSeconds(
        PluginConfiguration config)
    {
        var configured =
            config.PlaybackStartupWaitSeconds;

        if (configured <= 0)
        {
            return DefaultLibraryDiscoveryWaitSeconds;
        }

        return Math.Clamp(
            configured,
            10,
            300);
    }

    private async Task<GPhotosDlService.DownloadStatus?>
        TryGetDownloadStatusAsync(
            string mediaVaultId,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _gphotosDl.GetStatusAsync(
                    mediaVaultId,
                    timeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to query GPhotosDL status for Media Vault item {MediaVaultId}.",
                mediaVaultId);

            return null;
        }
    }

    private async Task<GPhotosDlService.DownloadStatus?>
        TryGetDownloadStatusAsync(
            CloudMovieDto movie,
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _gphotosDl.GetStatusAsync(
                    movie.MediaVaultId,
                    timeoutSeconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to query GPhotosDL status for {Title}.",
                movie.Title);

            return null;
        }
    }

    // ========================================================================
    // CLIENT COMPATIBILITY
    // ========================================================================

    private IEnumerable<MediaSourceInfo>
        BuildClientCompatibleMediaSources(
            MediaSourceInfo realMediaSource,
            string title,
            string requestedItemId)
    {
        var channelItemId =
            TryResolveChannelItemId();

        var channelIdText =
            channelItemId != Guid.Empty
                ? channelItemId.ToString(
                    "N",
                    CultureInfo.InvariantCulture)
                : null;

        var exposedAliasIds =
            new List<string>();

        // Android TV compatibility.
        if (!string.Equals(
                realMediaSource.Id,
                requestedItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            var itemAlias =
                TryCloneMediaSource(
                    realMediaSource,
                    title);

            if (itemAlias is not null)
            {
                itemAlias.DefaultAudioIndexSource =
                    realMediaSource.DefaultAudioIndexSource;

                itemAlias.TranscodeReasons =
                    realMediaSource.TranscodeReasons;

                itemAlias.Id =
                    requestedItemId;

                exposedAliasIds.Add(
                    requestedItemId);

                yield return itemAlias;
            }
        }

        // Tizen / other client compatibility.
        if (channelIdText is not null
            &&
            !string.Equals(
                realMediaSource.Id,
                channelIdText,
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.Equals(
                requestedItemId,
                channelIdText,
                StringComparison.OrdinalIgnoreCase))
        {
            var channelAlias =
                TryCloneMediaSource(
                    realMediaSource,
                    title);

            if (channelAlias is not null)
            {
                channelAlias.DefaultAudioIndexSource =
                    realMediaSource.DefaultAudioIndexSource;

                channelAlias.TranscodeReasons =
                    realMediaSource.TranscodeReasons;

                channelAlias.Id =
                    channelIdText;

                exposedAliasIds.Add(
                    channelIdText);

                yield return channelAlias;
            }
        }

        if (exposedAliasIds.Any())
        {
            _logger.LogInformation(
                "Exposed channel media-source aliases for {Title}: " +
                "original={OriginalId}, aliases=[{Aliases}], channelName={ChannelName}.",
                title,
                realMediaSource.Id,
                string.Join(
                    ", ",
                    exposedAliasIds),
                Name);
        }
        else
        {
            _logger.LogWarning(
                "Could not build compatible alias sources for {Title}. " +
                "Returning only the original REAL media source (ID: {OriginalId}).",
                title,
                realMediaSource.Id);
        }

        // Always expose the original real Jellyfin source.
        yield return realMediaSource;
    }

    private Guid TryResolveChannelItemId()
    {
        try
        {
            var channelId =
                _libraryManager.GetNewItemId(
                    "Channel " + Name,
                    typeof(Channel));

            var channel =
                _libraryManager.GetItemById(
                    channelId);

            if (channel is Channel)
            {
                _logger.LogDebug(
                    "Resolved Media Vault channel {ChannelName} to Jellyfin " +
                    "Channel BaseItem.Id {ChannelBaseItemId}.",
                    Name,
                    channelId);

                return channelId;
            }

            _logger.LogWarning(
                "Jellyfin returned no Channel BaseItem for deterministic " +
                "ID {ChannelBaseItemId} ({ChannelName}).",
                channelId,
                Name);

            return channelId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to resolve the Jellyfin Channel BaseItem.Id for {ChannelName}.",
                Name);

            return Guid.Empty;
        }
    }

    private MediaSourceInfo? TryCloneMediaSource(
        MediaSourceInfo realMediaSource,
        string title)
    {
        try
        {
            return JsonSerializer.Deserialize<MediaSourceInfo>(
                JsonSerializer.SerializeToUtf8Bytes(
                    realMediaSource));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to clone Jellyfin media source {MediaSourceId} for " +
                "channel-ID playback compatibility of {Title}.",
                realMediaSource.Id,
                title);

            return null;
        }
    }

    private void LogResolvedMediaSource(
        string movieTitle,
        MediaSourceInfo mediaSource,
        string sourceDescription)
    {
        _logger.LogInformation(
            "Using REAL Jellyfin media source {MediaSourceId} for Media Vault " +
            "movie {Title} via {SourceDescription}. Protocol={Protocol}, " +
            "Container={Container}, Size={Size}, Bitrate={Bitrate}, " +
            "Streams={StreamCount}, Path={Path}.",
            mediaSource.Id,
            movieTitle,
            sourceDescription,
            mediaSource.Protocol,
            mediaSource.Container,
            mediaSource.Size,
            mediaSource.Bitrate,
            mediaSource.MediaStreams?.Count ?? 0,
            mediaSource.Path);
    }

    public Task<DynamicImageResponse> GetChannelImage(
        ImageType type,
        CancellationToken cancellationToken) =>
        Task.FromResult(
            new DynamicImageResponse
            {
                HasImage = false
            });

    public IEnumerable<ImageType>
        GetSupportedChannelImages() => [];
}