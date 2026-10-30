using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using MediaVault.Jellyfin.Catalogue;
using MediaVault.Jellyfin.Channels;
using MediaVault.Jellyfin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.Jellyfin;

/// <summary>
/// Registers Media Vault services with Jellyfin.
/// </summary>
public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        // Shared services.
        serviceCollection.AddSingleton<CatalogueStore>();
        serviceCollection.AddSingleton<GPhotosDlService>();

        // Media Vault channels.
        //
        // MediaVaultChannel requires MediaVaultChannelKind in its constructor,
        // so each channel must be registered through its concrete wrapper.
        serviceCollection.AddSingleton<IChannel, MediaVaultMoviesChannel>();
        serviceCollection.AddSingleton<IChannel, MediaVaultTvShowsChannel>();
        serviceCollection.AddSingleton<IChannel, MediaVaultFormula1Channel>();

        // Keeps the catalogue refreshed from the Media Vault API.
        serviceCollection.AddHostedService<CatalogueHostedService>();

        // Keeps Jellyfin's actual Channel Movie sort names synchronised with
        // the catalogue's sortTitle values.
        //
        // Register the concrete service as a singleton AND expose that same
        // instance as the hosted service, so CatalogueController can inject it.
        serviceCollection.AddSingleton<MediaVaultSortTitleHostedService>();
        serviceCollection.AddHostedService(sp =>
            sp.GetRequiredService<MediaVaultSortTitleHostedService>());
    }
}