using MediaBrowser.Model.Plugins;

namespace MediaVault.Jellyfin.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string CatalogueSource { get; set; } = string.Empty;
    public string GPhotosDlUrl { get; set; } = string.Empty;
    public string MoviesDownloadDirectory { get; set; } = string.Empty;
    public string TvShowsDownloadDirectory { get; set; } = string.Empty;
    public string F1DownloadDirectory { get; set; } = string.Empty;
    public int RefreshIntervalHours { get; set; } = 6;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int LibraryRefreshDelaySeconds { get; set; } = 10;

    // Playback startup settings.
    public int PlaybackStartupWaitSeconds { get; set; } = 120;
    public int PlaybackStartupBufferPercent { get; set; } = 5;
    public double PlaybackStartupMinimumBufferGB { get; set; } = 0.5;
    public double PlaybackStartupMaximumBufferGB { get; set; } = 3.0;
}

