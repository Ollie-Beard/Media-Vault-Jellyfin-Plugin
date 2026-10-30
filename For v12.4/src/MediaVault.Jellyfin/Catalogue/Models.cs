using System.Text.Json.Serialization;

namespace MediaVault.Jellyfin.Catalogue;

public sealed class MediaVaultExport
{
    [JsonPropertyName("revision")] public long Revision { get; init; }
    [JsonPropertyName("generatedAt")] public DateTimeOffset? GeneratedAt { get; init; }
    [JsonPropertyName("movies")] public List<MediaVaultMovie>? Movies { get; init; }
    [JsonPropertyName("tvShows")] public List<MediaVaultTvShow>? TvShows { get; init; }
    [JsonPropertyName("f1")] public List<MediaVaultF1Season>? F1 { get; init; }
}

public sealed class MediaVaultMovie
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("sortTitle")] public string? SortTitle { get; init; }
    [JsonPropertyName("releaseDate")] public string? ReleaseDate { get; init; }
    [JsonPropertyName("ageRating")] public string? AgeRating { get; init; }
    [JsonPropertyName("genres")] public List<string>? Genres { get; init; }
    [JsonPropertyName("collections")] public List<string>? Collections { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("runtimeMinutes")] public int? RuntimeMinutes { get; init; }
    [JsonPropertyName("tmdbId")] public int? TmdbId { get; init; }
    [JsonPropertyName("posterUrl")] public string? PosterUrl { get; init; }
    [JsonPropertyName("backdropUrl")] public string? BackdropUrl { get; init; }
    [JsonPropertyName("cast")] public List<string>? Cast { get; init; }
    [JsonPropertyName("director")] public string? Director { get; init; }
    [JsonPropertyName("rating")] public double? Rating { get; init; }
    [JsonPropertyName("downloadedToPc")] public bool DownloadedToPc { get; init; }
    [JsonPropertyName("fileSizeGb")] public double? FileSizeGb { get; init; }
    [JsonPropertyName("resolution")] public string? Resolution { get; init; }
    [JsonPropertyName("medium")] public string? Medium { get; init; }
    [JsonPropertyName("googlePhotosLink")] public string? GooglePhotosLink { get; init; }
}

public sealed class MediaVaultTvShow
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("sortTitle")] public string? SortTitle { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("releaseDate")] public string? ReleaseDate { get; init; }
    [JsonPropertyName("ageRating")] public string? AgeRating { get; init; }
    [JsonPropertyName("genres")] public List<string>? Genres { get; init; }
    [JsonPropertyName("collections")] public List<string>? Collections { get; init; }
    [JsonPropertyName("tmdbId")] public int? TmdbId { get; init; }
    [JsonPropertyName("posterUrl")] public string? PosterUrl { get; init; }
    [JsonPropertyName("backdropUrl")] public string? BackdropUrl { get; init; }
    [JsonPropertyName("rating")] public double? Rating { get; init; }
    [JsonPropertyName("isOngoing")] public bool IsOngoing { get; init; }
    [JsonPropertyName("googlePhotosLink")] public string? GooglePhotosLink { get; init; }
    [JsonPropertyName("totalSeasons")] public int? TotalSeasons { get; init; }
    [JsonPropertyName("seasonCount")] public int? SeasonCount { get; init; }
    [JsonPropertyName("seasons")] public List<MediaVaultTvSeason>? Seasons { get; init; }
}

public sealed class MediaVaultTvSeason
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("seasonNumber")] public int SeasonNumber { get; init; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("countsAsSeason")] public bool CountsAsSeason { get; init; } = true;
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; init; }
    [JsonPropertyName("firstEpisodeDate")] public string? FirstEpisodeDate { get; init; }
    [JsonPropertyName("lastEpisodeDate")] public string? LastEpisodeDate { get; init; }
    [JsonPropertyName("fileSizeGb")] public double? FileSizeGb { get; init; }
    [JsonPropertyName("resolution")] public string? Resolution { get; init; }
    [JsonPropertyName("medium")] public string? Medium { get; init; }
    [JsonPropertyName("episodeCount")] public int? EpisodeCount { get; init; }
    [JsonPropertyName("countedEpisodeCount")] public int? CountedEpisodeCount { get; init; }
    [JsonPropertyName("episodes")] public List<MediaVaultTvEpisode>? Episodes { get; init; }
}

public sealed class MediaVaultTvEpisode
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("episodeNumber")] public int? EpisodeNumber { get; init; }
    [JsonPropertyName("displayEpisodeNumber")] public int? DisplayEpisodeNumber { get; init; }
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; init; }
    [JsonPropertyName("tmdbEpisodeId")] public int? TmdbEpisodeId { get; init; }
    [JsonPropertyName("airDate")] public string? AirDate { get; init; }
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("runtimeMinutes")] public int? RuntimeMinutes { get; init; }
    [JsonPropertyName("posterUrl")] public string? PosterUrl { get; init; }
    [JsonPropertyName("stillUrl")] public string? StillUrl { get; init; }
    [JsonPropertyName("rating")] public double? Rating { get; init; }
    [JsonPropertyName("fileSizeGb")] public double? FileSizeGb { get; init; }
    [JsonPropertyName("resolution")] public string? Resolution { get; init; }
    [JsonPropertyName("medium")] public string? Medium { get; init; }
    [JsonPropertyName("bestConsumerResolution")] public string? BestConsumerResolution { get; init; }
    [JsonPropertyName("resolutionGotItIn")] public string? ResolutionGotItIn { get; init; }
    [JsonPropertyName("bestConsumerMedium")] public string? BestConsumerMedium { get; init; }
    [JsonPropertyName("mediumGotItIn")] public string? MediumGotItIn { get; init; }
    [JsonPropertyName("googlePhotosLink")] public string? GooglePhotosLink { get; init; }
    [JsonPropertyName("countsAsEpisode")] public bool CountsAsEpisode { get; init; } = true;
}

public sealed class MediaVaultF1Season
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("year")] public int Year { get; init; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; init; }
    [JsonPropertyName("rounds")] public List<MediaVaultF1Round>? Rounds { get; init; }
}

public sealed class MediaVaultF1Round
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("place")] public string? Place { get; init; }
    [JsonPropertyName("grandPrixName")] public string? GrandPrixName { get; init; }
    [JsonPropertyName("date")] public string? Date { get; init; }
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; init; }
    [JsonPropertyName("events")] public List<MediaVaultF1Event>? Events { get; init; }
}

public sealed class MediaVaultF1Event
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("mediaVaultId")] public string? MediaVaultId { get; init; }
    [JsonPropertyName("eventType")] public string? EventType { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("date")] public string? Date { get; init; }
    [JsonPropertyName("runtimeMinutes")] public int? RuntimeMinutes { get; init; }
    [JsonPropertyName("fileSizeGb")] public double? FileSizeGb { get; init; }
    [JsonPropertyName("resolution")] public string? Resolution { get; init; }
    [JsonPropertyName("medium")] public string? Medium { get; init; }
    [JsonPropertyName("bestConsumerResolution")] public string? BestConsumerResolution { get; init; }
    [JsonPropertyName("resolutionGotItIn")] public string? ResolutionGotItIn { get; init; }
    [JsonPropertyName("bestConsumerMedium")] public string? BestConsumerMedium { get; init; }
    [JsonPropertyName("mediumGotItIn")] public string? MediumGotItIn { get; init; }
    [JsonPropertyName("googlePhotosLink")] public string? GooglePhotosLink { get; init; }
    [JsonPropertyName("filenameBase")] public string? FilenameBase { get; init; }
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; init; }
}

public sealed record CloudMovieDto(
    Guid Id, string MediaVaultId, string Title, string? SortTitle, string? ReleaseDate,
    string? AgeRating, IReadOnlyList<string> Genres, IReadOnlyList<string> Collections,
    string? Overview, int? RuntimeMinutes, int? TmdbId, string? PosterUrl,
    string? BackdropUrl, IReadOnlyList<string> Cast, string? Director, double? Rating,
    bool DownloadedToPc, string? GooglePhotosLink, double? FileSizeGb,
    string? Resolution, string? Medium);

public sealed record CloudTvShowDto(
    Guid Id, string MediaVaultId, string Title, string? SortTitle, string? ReleaseDate,
    string? AgeRating, IReadOnlyList<string> Genres, IReadOnlyList<string> Collections,
    string? Overview, int? TmdbId, string? PosterUrl, string? BackdropUrl, double? Rating,
    bool IsOngoing, string? GooglePhotosLink, int? TotalSeasons, int? SeasonCount,
    IReadOnlyList<CloudTvSeasonDto> Seasons);

public sealed record CloudTvSeasonDto(
    Guid Id, string MediaVaultId, int SeasonNumber, string DisplayName,
    bool CountsAsSeason, int DisplayOrder, string? FirstEpisodeDate,
    string? LastEpisodeDate, double? FileSizeGb, string? Resolution, string? Medium,
    int? EpisodeCount, int? CountedEpisodeCount, IReadOnlyList<CloudTvEpisodeDto> Episodes);

public sealed record CloudTvEpisodeDto(
    Guid Id, string MediaVaultId, string Title, int? EpisodeNumber,
    int? DisplayEpisodeNumber, int DisplayOrder, int? TmdbEpisodeId, string? AirDate,
    string? Overview, int? RuntimeMinutes, string? PosterUrl, string? StillUrl,
    double? Rating, double? FileSizeGb, string? Resolution, string? Medium,
    string? BestConsumerResolution, string? ResolutionGotItIn, string? BestConsumerMedium,
    string? MediumGotItIn, string? GooglePhotosLink, bool CountsAsEpisode);

public sealed record CloudF1SeasonDto(
    Guid Id, string MediaVaultId, int Year, string DisplayName, int DisplayOrder,
    IReadOnlyList<CloudF1RoundDto> Rounds);

public sealed record CloudF1RoundDto(
    Guid Id, string MediaVaultId, int RoundNumber, string Name, string? Place,
    string? GrandPrixName, string? Date, int DisplayOrder,
    IReadOnlyList<CloudF1EventDto> Events);

public sealed record CloudF1EventDto(
    Guid Id, string MediaVaultId, string EventType, string Title, string? Date,
    int? RuntimeMinutes, double? FileSizeGb, string? Resolution, string? Medium,
    string? BestConsumerResolution, string? ResolutionGotItIn, string? BestConsumerMedium,
    string? MediumGotItIn, string? GooglePhotosLink, string? FilenameBase, int DisplayOrder);

public sealed record RefreshResult(
    bool Success, int ValidMovies, int InvalidMovies, string? Error, DateTimeOffset CompletedAt);

public sealed record CatalogueStatus(
    int Count, DateTimeOffset? LastSuccessfulRefresh, string? LastError, long Revision);


