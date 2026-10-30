using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaVault.Jellyfin.Catalogue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaVault.Jellyfin.Services;

/// <summary>
/// Keeps Media Vault Channel Movie sort names in sync with the catalogue.
///
/// Jellyfin does not expose a SortName property on ChannelItemInfo. The
/// actual Channel Movie BaseItem is created by Jellyfin's ChannelManager,
/// so the plugin updates that BaseItem after Jellyfin has created/updated it.
///
/// The catalogue's sortTitle is written to BaseItem.ForcedSortName, which is
/// the native Jellyfin field used to generate SortName.
///
/// This service only reads/updates existing BaseItems in Jellyfin's database.
/// It never queues or performs a library scan.
/// </summary>
public sealed class MediaVaultSortTitleHostedService : IHostedService
{
    private const string MoviesChannelName = "Cinedex Movies";
    private const string MediaVaultProviderId = "MediaVault";

    private readonly CatalogueStore _catalogueStore;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MediaVaultSortTitleHostedService> _logger;

    private Guid _moviesChannelId;

    public MediaVaultSortTitleHostedService(
        CatalogueStore catalogueStore,
        ILibraryManager libraryManager,
        ILogger<MediaVaultSortTitleHostedService> logger)
    {
        _catalogueStore = catalogueStore;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _moviesChannelId = _libraryManager.GetNewItemId(
            "Channel " + MoviesChannelName,
            typeof(Channel));

        _libraryManager.ItemAdded += OnLibraryItemChanged;
        _libraryManager.ItemUpdated += OnLibraryItemChanged;

        _logger.LogInformation(
            "Media Vault sort-title service started for Channel {ChannelName} ({ChannelId}).",
            MoviesChannelName,
            _moviesChannelId);

        // Do not hold up Jellyfin startup while reconciling existing Channel
        // Movies. The reconciliation is a targeted database query/update pass,
        // not a library scan.
        _ = ReconcileExistingMoviesAsync();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnLibraryItemChanged;
        _libraryManager.ItemUpdated -= OnLibraryItemChanged;

        _logger.LogInformation(
            "Media Vault sort-title service stopped.");

        return Task.CompletedTask;
    }

    private void OnLibraryItemChanged(
        object? sender,
        ItemChangeEventArgs e)
    {
        if (e.Item is not Movie movie
            || movie.ChannelId != _moviesChannelId
            || !movie.ProviderIds.TryGetValue(
                MediaVaultProviderId,
                out var mediaVaultId)
            || string.IsNullOrWhiteSpace(mediaVaultId))
        {
            return;
        }

        _ = ApplyCatalogueSortTitleAsync(
            movie,
            mediaVaultId,
            CancellationToken.None);
    }

    public async Task ReconcileExistingMoviesAsync()
    {
        try
        {
            var existingMovies = _libraryManager.GetItemList(
                new InternalItemsQuery
                {
                    ChannelIds = [_moviesChannelId],
                    IncludeItemTypes = [BaseItemKind.Movie],
                    IsFolder = false
                });

            _logger.LogInformation(
                "Media Vault sort-title reconciliation found {Count} existing Channel Movies.",
                existingMovies.Count);

            var applied = 0;

            foreach (var item in existingMovies)
            {
                if (item is not Movie movie
                    || !movie.ProviderIds.TryGetValue(
                        MediaVaultProviderId,
                        out var mediaVaultId)
                    || string.IsNullOrWhiteSpace(mediaVaultId))
                {
                    continue;
                }

                if (await ApplyCatalogueSortTitleAsync(
                        movie,
                        mediaVaultId,
                        CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    applied++;
                }
            }

            _logger.LogInformation(
                "Media Vault sort-title reconciliation completed. Updated {UpdatedCount} Channel Movies.",
                applied);
        }
        catch (OperationCanceledException)
        {
            // No cancellation token is used for the background reconciliation.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Media Vault sort-title reconciliation failed.");
        }
    }

    private async Task<bool> ApplyCatalogueSortTitleAsync(
        Movie movie,
        string mediaVaultId,
        CancellationToken cancellationToken)
    {
        var catalogueMovie = _catalogueStore.Movies.FirstOrDefault(
            x => string.Equals(
                x.MediaVaultId,
                mediaVaultId,
                StringComparison.OrdinalIgnoreCase));

        if (catalogueMovie is null
            || string.IsNullOrWhiteSpace(catalogueMovie.SortTitle))
        {
            return false;
        }

        var desiredSortTitle = catalogueMovie.SortTitle.Trim();

        if (string.Equals(
                movie.ForcedSortName,
                desiredSortTitle,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _logger.LogInformation(
                "Applying Media Vault sortTitle {SortTitle} to Channel Movie " +
                "{Title} ({ItemId}, MediaVaultId={MediaVaultId}).",
                desiredSortTitle,
                movie.Name,
                movie.Id,
                mediaVaultId);

            // ForcedSortName is Jellyfin's native override used by BaseItem.SortName.
            // Setting it also invalidates the cached SortName in BaseItem.
            movie.ForcedSortName = desiredSortTitle;

            await movie.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Media Vault sortTitle {SortTitle} persisted for Channel Movie " +
                "{Title} ({ItemId}). Jellyfin SortName is now {SortName}.",
                desiredSortTitle,
                movie.Name,
                movie.Id,
                movie.SortName);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist Media Vault sortTitle {SortTitle} for Channel Movie " +
                "{Title} ({ItemId}).",
                desiredSortTitle,
                movie.Name,
                movie.Id);

            return false;
        }
    }
}
