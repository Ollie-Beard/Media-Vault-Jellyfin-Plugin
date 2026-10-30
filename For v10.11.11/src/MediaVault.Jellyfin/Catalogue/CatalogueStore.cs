using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaVault.Jellyfin.Configuration;

namespace MediaVault.Jellyfin.Catalogue;

public sealed class CatalogueStore
{
    private const string CacheFile = "media-vault-catalogue.json";

    private static readonly Guid NamespaceId =
        Guid.Parse("f0413623-c78e-45e7-a114-352b0bfb205f");

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private List<CloudMovieDto> _movies = [];
    private List<CloudTvShowDto> _tvShows = [];
    private List<CloudF1SeasonDto> _f1Seasons = [];

    private DateTimeOffset? _lastSuccessfulRefresh;
    private string? _lastError;
    private long _revision;

    public IReadOnlyList<CloudMovieDto> Movies => _movies;
    public IReadOnlyList<CloudTvShowDto> TvShows => _tvShows;
    public IReadOnlyList<CloudF1SeasonDto> F1Seasons => _f1Seasons;

    public CatalogueStatus Status =>
        new(
            _movies.Count,
            _lastSuccessfulRefresh,
            _lastError,
            _revision);

    public async Task<RefreshResult> RefreshAsync(
        CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance
            ?? throw new InvalidOperationException(
                "Plugin is not initialized.");

        var config = plugin.Configuration;

        if (string.IsNullOrWhiteSpace(config.CatalogueSource))
        {
            return Fail("No catalogue source is configured.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var text = await ReadSourceAsync(
                    config,
                    cancellationToken)
                .ConfigureAwait(false);

            var export = JsonSerializer.Deserialize<MediaVaultExport>(
                text,
                _json);

            if (export is null)
            {
                return Fail(
                    "Catalogue root is invalid: unable to deserialize the response.");
            }

            // Movies remain mandatory because this is the existing catalogue
            // contract. TV/F1 are optional until the website exposes them.
            if (export.Movies is null)
            {
                return Fail(
                    "Catalogue root is invalid: expected a movies array.");
            }

            var seenMovies = new HashSet<string>(StringComparer.Ordinal);
            var validMovies = new List<CloudMovieDto>();
            var invalidMovies = 0;

            foreach (var movie in export.Movies)
            {
                if (!TryMapMovie(movie, seenMovies, out var mapped))
                {
                    invalidMovies++;
                    continue;
                }

                if (mapped is not null)
                {
                    validMovies.Add(mapped);
                }
            }

            _movies = validMovies
                .OrderBy(
                    x => x.SortTitle ?? x.Title,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            _tvShows = MapTvShows(export.TvShows)
                .OrderBy(
                    x => x.SortTitle ?? x.Title,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            _f1Seasons = MapF1Seasons(export.F1)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Year)
                .ToList();

            _lastSuccessfulRefresh = DateTimeOffset.UtcNow;
            _lastError = null;
            _revision++;

            var cachePath = Path.Combine(
                plugin.DataFolderPath,
                CacheFile);

            Directory.CreateDirectory(plugin.DataFolderPath);

            var cache = new CatalogueCache(
                _movies,
                _tvShows,
                _f1Seasons);

            await File.WriteAllTextAsync(
                    cachePath + ".tmp",
                    JsonSerializer.Serialize(cache, _json),
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(cachePath + ".tmp", cachePath, true);

            return new RefreshResult(
                true,
                validMovies.Count,
                invalidMovies,
                null,
                _lastSuccessfulRefresh.Value);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException)
        {
            return Fail(
                "Refresh failed; retained last known good catalogue. "
                + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task LoadCachedAsync(
        CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;

        if (plugin is null)
        {
            return;
        }

        var path = Path.Combine(
            plugin.DataFolderPath,
            CacheFile);

        if (!File.Exists(path))
        {
            return;
        }

        var data = await File.ReadAllTextAsync(
                path,
                cancellationToken)
            .ConfigureAwait(false);

        // New cache format: one envelope containing all catalogue types.
        try
        {
            var cache = JsonSerializer.Deserialize<CatalogueCache>(
                data,
                _json);

            if (cache is not null && cache.Movies is not null)
            {
                _movies = cache.Movies.ToList();
                _tvShows = cache.TvShows?.ToList() ?? [];
                _f1Seasons = cache.F1Seasons?.ToList() ?? [];
                _revision++;
                return;
            }
        }
        catch (JsonException)
        {
            // Fall through to the legacy movie-only cache format.
        }

        // Older versions stored a bare List<CloudMovieDto>. Keep accepting it
        // so the working movie catalogue survives the model expansion.
        var legacyMovies = JsonSerializer.Deserialize<List<CloudMovieDto>>(
            data,
            _json);

        _movies = legacyMovies ?? [];
        _tvShows = [];
        _f1Seasons = [];
        _revision++;
    }

    private RefreshResult Fail(string error)
    {
        _lastError = error;

        return new RefreshResult(
            false,
            _movies.Count,
            0,
            error,
            DateTimeOffset.UtcNow);
    }

    private static async Task<string> ReadSourceAsync(
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var source = config.CatalogueSource.Trim();

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException(
                "No catalogue source is configured.");
        }

        if (Path.IsPathRooted(source))
        {
            var fullPath = Path.GetFullPath(source);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Catalogue file was not found: {fullPath}",
                    fullPath);
            }

            return await File.ReadAllTextAsync(
                    fullPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (Uri.TryCreate(
                source,
                UriKind.Absolute,
                out var uri))
        {
            if (uri.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException(
                    "Only HTTP(S) catalogue URLs are accepted.");
            }

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(
                    Math.Clamp(
                        config.RequestTimeoutSeconds,
                        5,
                        120))
            };

            return await client.GetStringAsync(
                    uri,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var relativePath = Path.GetFullPath(source);

        if (!File.Exists(relativePath))
        {
            throw new FileNotFoundException(
                $"Catalogue file was not found: {relativePath}",
                relativePath);
        }

        return await File.ReadAllTextAsync(
                relativePath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryMapMovie(
        MediaVaultMovie movie,
        HashSet<string> seen,
        out CloudMovieDto? mapped)
    {
        mapped = null;

        var mediaVaultId = movie.MediaVaultId ?? movie.Id;

        if (string.IsNullOrWhiteSpace(mediaVaultId)
            || string.IsNullOrWhiteSpace(movie.Title)
            || !seen.Add(mediaVaultId))
        {
            return false;
        }

        mapped = new CloudMovieDto(
            StableId(mediaVaultId),
            mediaVaultId,
            movie.Title,
            movie.SortTitle,
            movie.ReleaseDate,
            movie.AgeRating,
            movie.Genres ?? [],
            movie.Collections ?? [],
            movie.Overview,
            movie.RuntimeMinutes,
            movie.TmdbId,
            movie.PosterUrl,
            movie.BackdropUrl,
            movie.Cast ?? [],
            movie.Director,
            movie.Rating,
            movie.DownloadedToPc,
            movie.GooglePhotosLink,
            movie.FileSizeGb,
            movie.Resolution,
            movie.Medium);

        return true;
    }

    private static List<CloudTvShowDto> MapTvShows(
        IEnumerable<MediaVaultTvShow>? shows)
    {
        if (shows is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CloudTvShowDto>();

        foreach (var show in shows)
        {
            var mediaVaultId = show.MediaVaultId ?? show.Id;

            if (string.IsNullOrWhiteSpace(mediaVaultId)
                || string.IsNullOrWhiteSpace(show.Title)
                || !seen.Add(mediaVaultId))
            {
                continue;
            }

            result.Add(new CloudTvShowDto(
                StableId(mediaVaultId),
                mediaVaultId,
                show.Title,
                show.SortTitle,
                show.ReleaseDate,
                show.AgeRating,
                show.Genres ?? [],
                show.Collections ?? [],
                show.Overview,
                show.TmdbId,
                show.PosterUrl,
                show.BackdropUrl,
                show.Rating,
                show.IsOngoing,
                show.GooglePhotosLink,
                show.TotalSeasons,
                show.SeasonCount,
                MapTvSeasons(show.Seasons)));
        }

        return result;
    }

    private static List<CloudTvSeasonDto> MapTvSeasons(
        IEnumerable<MediaVaultTvSeason>? seasons)
    {
        if (seasons is null)
        {
            return [];
        }

        var result = new List<CloudTvSeasonDto>();
        var seen = new HashSet<int>();

        foreach (var season in seasons)
        {
            if (!seen.Add(season.SeasonNumber))
            {
                continue;
            }

            var mediaVaultId = season.MediaVaultId ?? season.Id ?? $"season:{season.SeasonNumber}";

            var displayName = string.IsNullOrWhiteSpace(season.DisplayName)
                ? $"Season {season.SeasonNumber}"
                : season.DisplayName.Trim();

            result.Add(new CloudTvSeasonDto(
                StableId(mediaVaultId),
                mediaVaultId,
                season.SeasonNumber,
                displayName,
                season.CountsAsSeason,
                season.DisplayOrder,
                season.FirstEpisodeDate,
                season.LastEpisodeDate,
                season.FileSizeGb,
                season.Resolution,
                season.Medium,
                season.EpisodeCount,
                season.CountedEpisodeCount,
                MapTvEpisodes(season.Episodes)));
        }

        return result
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.SeasonNumber)
            .ToList();
    }

    private static List<CloudTvEpisodeDto> MapTvEpisodes(
        IEnumerable<MediaVaultTvEpisode>? episodes)
    {
        if (episodes is null)
        {
            return [];
        }

        var result = new List<CloudTvEpisodeDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var episode in episodes)
        {
            var mediaVaultId = episode.MediaVaultId ?? episode.Id;

            if (string.IsNullOrWhiteSpace(mediaVaultId)
                || string.IsNullOrWhiteSpace(episode.Title)
                || !seen.Add(mediaVaultId))
            {
                continue;
            }

            result.Add(new CloudTvEpisodeDto(
                StableId(mediaVaultId),
                mediaVaultId,
                episode.Title,
                episode.EpisodeNumber,
                episode.DisplayEpisodeNumber,
                episode.DisplayOrder,
                episode.TmdbEpisodeId,
                episode.AirDate,
                episode.Overview,
                episode.RuntimeMinutes,
                episode.PosterUrl,
                episode.StillUrl,
                episode.Rating,
                episode.FileSizeGb,
                episode.Resolution,
                episode.Medium,
                episode.BestConsumerResolution,
                episode.ResolutionGotItIn,
                episode.BestConsumerMedium,
                episode.MediumGotItIn,
                episode.GooglePhotosLink,
                episode.CountsAsEpisode));
        }

        return result
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.DisplayEpisodeNumber ?? x.EpisodeNumber ?? int.MaxValue)
            .ToList();
    }

    private static List<CloudF1SeasonDto> MapF1Seasons(
        IEnumerable<MediaVaultF1Season>? seasons)
    {
        if (seasons is null)
        {
            return [];
        }

        var result = new List<CloudF1SeasonDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var season in seasons)
        {
            var mediaVaultId = season.MediaVaultId ?? season.Id;

            if (string.IsNullOrWhiteSpace(mediaVaultId)
                || !seen.Add(mediaVaultId))
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(season.DisplayName)
                ? season.Year.ToString()
                : season.DisplayName.Trim();

            result.Add(new CloudF1SeasonDto(
                StableId(mediaVaultId),
                mediaVaultId,
                season.Year,
                displayName,
                season.DisplayOrder,
                MapF1Rounds(season.Rounds)));
        }

        return result
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Year)
            .ToList();
    }

    private static List<CloudF1RoundDto> MapF1Rounds(
        IEnumerable<MediaVaultF1Round>? rounds)
    {
        if (rounds is null)
        {
            return [];
        }

        var result = new List<CloudF1RoundDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var round in rounds)
        {
            var mediaVaultId = round.MediaVaultId ?? round.Id;

            if (string.IsNullOrWhiteSpace(mediaVaultId)
                || !seen.Add(mediaVaultId))
            {
                continue;
            }

            var name = FirstNonEmpty(
                round.Title,
                round.DisplayName,
                round.Name,
                round.GrandPrixName,
                $"Round {round.RoundNumber}");

            result.Add(new CloudF1RoundDto(
                StableId(mediaVaultId),
                mediaVaultId,
                round.RoundNumber,
                name,
                round.Place,
                round.GrandPrixName,
                round.Date,
                round.DisplayOrder,
                MapF1Events(round.Events)));
        }

        return result
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.RoundNumber)
            .ToList();
    }

    private static List<CloudF1EventDto> MapF1Events(
        IEnumerable<MediaVaultF1Event>? events)
    {
        if (events is null)
        {
            return [];
        }

        var result = new List<CloudF1EventDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var @event in events)
        {
            var mediaVaultId = @event.MediaVaultId ?? @event.Id;

            if (string.IsNullOrWhiteSpace(mediaVaultId)
                || string.IsNullOrWhiteSpace(@event.EventType)
                || !seen.Add(mediaVaultId))
            {
                continue;
            }

            var title = FirstNonEmpty(
                @event.Title,
                @event.EventType,
                "F1 Event");

            result.Add(new CloudF1EventDto(
                StableId(mediaVaultId),
                mediaVaultId,
                @event.EventType.Trim(),
                title,
                @event.Date,
                @event.RuntimeMinutes,
                @event.FileSizeGb,
                @event.Resolution,
                @event.Medium,
                @event.BestConsumerResolution,
                @event.ResolutionGotItIn,
                @event.BestConsumerMedium,
                @event.MediumGotItIn,
                @event.GooglePhotosLink,
                @event.FilenameBase,
                @event.DisplayOrder));
        }

        return result
            .OrderBy(x => F1EventOrder(x.EventType))
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim()
        ?? string.Empty;

    public static Guid StableId(string mediaVaultId)
    {
        Span<byte> bytes = stackalloc byte[16];
        NamespaceId.TryWriteBytes(bytes);

        var name = Encoding.UTF8.GetBytes(mediaVaultId);
        var hash = SHA1.HashData(
            bytes.ToArray()
                .Concat(name)
                .ToArray());

        var guid = hash[..16];
        guid[6] = (byte)((guid[6] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    public static bool IsCloudEligible(MediaVaultMovie movie) =>
        !string.IsNullOrWhiteSpace(movie.Id)
        && !string.IsNullOrWhiteSpace(movie.Title);

    public static string? TmdbImage(string? path, string size) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : path.StartsWith('/')
                ? "https://image.tmdb.org/t/p/" + size + path
                : null;

    private sealed record CatalogueCache(
        IReadOnlyList<CloudMovieDto> Movies,
        IReadOnlyList<CloudTvShowDto> TvShows,
        IReadOnlyList<CloudF1SeasonDto> F1Seasons);
}


