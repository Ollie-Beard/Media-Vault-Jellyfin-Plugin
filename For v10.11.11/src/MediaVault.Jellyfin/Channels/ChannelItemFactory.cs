using System.Globalization;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaVault.Jellyfin.Catalogue;

namespace MediaVault.Jellyfin.Channels;

/// <summary>
/// Maps the Media Vault catalogue into Jellyfin's Channel hierarchy.
/// Folder IDs are stable across renames to avoid duplicate Channel entries.
/// </summary>
public static class ChannelItemFactory
{
    public const string CloudMoviesFolderId = "media-vault-cloud-movies";
    public const string CloudTvShowsFolderId = "media-vault-cloud-tv";
    public const string CloudF1FolderId = "media-vault-cloud-f1";

    public static string TvShowFolderId(Guid id) => $"tv-show:{id:N}";
    public static string TvSeasonFolderId(Guid id) => $"tv-season:{id:N}";
    public static string F1SeasonFolderId(Guid id) => $"f1-season:{id:N}";
    public static string F1RoundFolderId(Guid id) => $"f1-round:{id:N}";
    public static string F1EventItemId(Guid id) => $"f1-event-v2:{id:N}";

    public static ChannelItemInfo CloudMoviesFolder(int movieCount) => Folder(
        CloudMoviesFolderId,
        $"{movieCount:N0} Movies from Vault");

    public static ChannelItemInfo CloudTvShowsFolder() => Folder(
        CloudTvShowsFolderId,
        "Cloud TV Shows");

    public static ChannelItemInfo CloudF1Folder() => Folder(
        CloudF1FolderId,
        "Formula 1");

    public static ChannelItemInfo Movie(CloudMovieDto movie)
    {
        ArgumentNullException.ThrowIfNull(movie);

        return new ChannelItemInfo
        {
            Id = movie.Id.ToString("N", CultureInfo.InvariantCulture),
            Name = movie.Title,
            Type = ChannelItemType.Media,
            ContentType = ChannelMediaContentType.Movie,
            MediaType = ChannelMediaType.Video,
            Overview = movie.Overview,
            OfficialRating = movie.AgeRating,
            Genres = movie.Genres.ToList(),
            Tags = BuildMovieTags(movie),
            PremiereDate = ParseDate(movie.ReleaseDate),
            ProductionYear = ParseYear(movie.ReleaseDate),
            RunTimeTicks = RuntimeTicks(movie.RuntimeMinutes),
            ProviderIds = MovieProviderIds(movie),
            People = People(movie),
            CommunityRating = Rating(movie.Rating),
            ImageUrl = movie.PosterUrl,
            DateModified = DateTime.UtcNow
        };
    }

    public static ChannelItemInfo TvShow(CloudTvShowDto show) => new()
    {
        Id = TvShowFolderId(show.Id),
        Name = show.Title,
        SeriesName = show.Title,
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Series,
        MediaType = ChannelMediaType.Video,
        Overview = show.Overview,
        OfficialRating = show.AgeRating,
        Genres = show.Genres.ToList(),
        CommunityRating = Rating(show.Rating),
        ImageUrl = show.PosterUrl,
        ProviderIds = ProviderIds(show.MediaVaultId, show.TmdbId),
        PremiereDate = ParseDate(show.ReleaseDate),
        ProductionYear = ParseYear(show.ReleaseDate),
        DateModified = DateTime.UtcNow
    };

    public static ChannelItemInfo TvSeason(
        CloudTvShowDto show,
        CloudTvSeasonDto season) => new()
    {
        Id = TvSeasonFolderId(season.Id),
        Name = season.DisplayName,
        SeriesName = show.Title,
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Season,
        MediaType = ChannelMediaType.Video,
        IndexNumber = season.SeasonNumber,
        ParentIndexNumber = season.SeasonNumber,
        ImageUrl = show.PosterUrl,
        DateModified = DateTime.UtcNow
    };

    public static ChannelItemInfo TvEpisode(
        CloudTvShowDto show,
        CloudTvSeasonDto season,
        CloudTvEpisodeDto episode) => new()
    {
        Id = episode.Id.ToString("N", CultureInfo.InvariantCulture),
        Name = episode.Title,
        SeriesName = show.Title,
        Type = ChannelItemType.Media,
        ContentType = ChannelMediaContentType.Episode,
        MediaType = ChannelMediaType.Video,
        Overview = episode.Overview,
        IndexNumber = episode.DisplayEpisodeNumber ?? episode.EpisodeNumber,
        ParentIndexNumber = season.SeasonNumber,
        OfficialRating = show.AgeRating,
        CommunityRating = Rating(episode.Rating),
        RunTimeTicks = RuntimeTicks(episode.RuntimeMinutes),
        PremiereDate = ParseDate(episode.AirDate),
        ProductionYear = ParseYear(episode.AirDate),
        ImageUrl = FirstNonEmpty(episode.StillUrl, episode.PosterUrl, show.PosterUrl),
        ProviderIds = EpisodeProviderIds(episode),
        Tags = BuildEpisodeTags(episode),
        DateModified = DateTime.UtcNow
    };

    public static ChannelItemInfo F1Season(CloudF1SeasonDto season) => new()
    {
        Id = F1SeasonFolderId(season.Id),
        Name = $"{season.Year} Season",
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Container,
        MediaType = ChannelMediaType.Video,
        IndexNumber = season.Year,
        DateModified = DateTime.UtcNow
    };

    public static ChannelItemInfo F1Round(
        CloudF1SeasonDto season,
        CloudF1RoundDto round) => new()
    {
        Id = F1RoundFolderId(round.Id),
        Name = FirstNonEmpty(
            round.Name,
            round.GrandPrixName,
            $"Round {round.RoundNumber}"),
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Container,
        MediaType = ChannelMediaType.Video,
        IndexNumber = round.RoundNumber,
        DateModified = DateTime.UtcNow
    };

    public static ChannelItemInfo F1Event(
        CloudF1SeasonDto season,
        CloudF1RoundDto round,
        CloudF1EventDto @event) => new()
    {
        Id = F1EventItemId(@event.Id),
        Name = @event.EventType,
        OriginalTitle = @event.Title,
        Type = ChannelItemType.Media,
        ContentType = ChannelMediaContentType.Movie,
        MediaType = ChannelMediaType.Video,
        Overview = FirstNonEmpty(round.GrandPrixName, round.Name),
        CommunityRating = null,
        RunTimeTicks = RuntimeTicks(@event.RuntimeMinutes),
        PremiereDate = ParseDate(@event.Date),
        ProductionYear = ParseYear(@event.Date) ?? season.Year,
        Tags = BuildF1Tags(@event),
        ImageUrl = null,
        ProviderIds = ProviderIds(@event.MediaVaultId, null),
        DateModified = DateTime.UtcNow
    };

    private static ChannelItemInfo Folder(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Container,
        MediaType = ChannelMediaType.Video,
        DateModified = DateTime.UtcNow
    };

    private static List<string> BuildMovieTags(CloudMovieDto movie)
    {
        var tags = movie.Collections
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => "Collection: " + x)
            .ToList();
        tags.Add("Media Vault");
        tags.Add(string.IsNullOrWhiteSpace(movie.GooglePhotosLink)
            ? "No Google Photos link"
            : "Cloud available");
        tags.Add(movie.DownloadedToPc
            ? "Also downloaded locally"
            : "Not downloaded locally");
        if (!string.IsNullOrWhiteSpace(movie.Resolution)) tags.Add("Resolution: " + movie.Resolution);
        if (!string.IsNullOrWhiteSpace(movie.Medium)) tags.Add("Medium: " + movie.Medium);
        if (movie.FileSizeGb is double size) tags.Add($"File size: {size:0.##} GB");
        return tags;
    }

    private static List<string> BuildEpisodeTags(CloudTvEpisodeDto episode)
    {
        var tags = new List<string> { "Media Vault" };
        tags.Add(string.IsNullOrWhiteSpace(episode.GooglePhotosLink)
            ? "No Google Photos link"
            : "Cloud available");
        if (!episode.CountsAsEpisode) tags.Add("Archive only");
        if (!string.IsNullOrWhiteSpace(episode.Resolution)) tags.Add("Resolution: " + episode.Resolution);
        if (!string.IsNullOrWhiteSpace(episode.Medium)) tags.Add("Medium: " + episode.Medium);
        if (episode.FileSizeGb is double size) tags.Add($"File size: {size:0.##} GB");
        return tags;
    }

    private static List<string> BuildF1Tags(CloudF1EventDto @event)
    {
        var tags = new List<string>
        {
            "Media Vault",
            "Formula 1",
            @event.EventType
        };
        if (string.IsNullOrWhiteSpace(@event.GooglePhotosLink)) tags.Add("No Google Photos link");
        else tags.Add("Cloud available");
        if (!string.IsNullOrWhiteSpace(@event.Resolution)) tags.Add("Resolution: " + @event.Resolution);
        if (!string.IsNullOrWhiteSpace(@event.Medium)) tags.Add("Medium: " + @event.Medium);
        if (@event.FileSizeGb is double size) tags.Add($"File size: {size:0.##} GB");
        return tags;
    }

    private static Dictionary<string, string> MovieProviderIds(CloudMovieDto movie) =>
        ProviderIds(movie.MediaVaultId, movie.TmdbId);

    private static Dictionary<string, string> EpisodeProviderIds(CloudTvEpisodeDto episode) =>
        ProviderIds(episode.MediaVaultId, episode.TmdbEpisodeId);

    private static Dictionary<string, string> ProviderIds(string mediaVaultId, int? tmdbId)
    {
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MediaVault"] = mediaVaultId
        };
        if (tmdbId is int value) ids["Tmdb"] = value.ToString(CultureInfo.InvariantCulture);
        return ids;
    }

    private static List<PersonInfo> People(CloudMovieDto movie)
    {
        var people = movie.Cast
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new PersonInfo { Name = x, Type = PersonKind.Actor })
            .ToList();
        if (!string.IsNullOrWhiteSpace(movie.Director))
            people.Add(new PersonInfo { Name = movie.Director, Type = PersonKind.Director });
        return people;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static int? ParseYear(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.Year
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
                ? year
                : null;

    private static long? RuntimeTicks(int? runtime) =>
        runtime is int minutes ? TimeSpan.FromMinutes(minutes).Ticks : null;

    private static float? Rating(double? rating) =>
        rating is double value ? (float)value : null;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
}