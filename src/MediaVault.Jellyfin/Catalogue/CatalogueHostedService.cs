using MediaVault.Jellyfin.Catalogue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaVault.Jellyfin.Services;

/// <summary>
/// Keeps the external JSON refresh independent of Channel browse requests.
/// </summary>
public sealed class CatalogueHostedService : IHostedService, IDisposable
{
    private readonly CatalogueStore _catalogueStore;
    private readonly ILogger<CatalogueHostedService> _logger;

    private CancellationTokenSource? _stopping;
    private Task? _loop;

    public CatalogueHostedService(
        CatalogueStore catalogueStore,
        ILogger<CatalogueHostedService> logger)
    {
        _catalogueStore = catalogueStore;
        _logger = logger;
    }

    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        _stopping =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        _loop =
            RunAsync(_stopping.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (_stopping is null || _loop is null)
        {
            return;
        }

        await _stopping
            .CancelAsync()
            .ConfigureAwait(false);

        try
        {
            await _loop
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when Jellyfin is shutting down.
        }
    }

    public void Dispose()
    {
        _stopping?.Dispose();
    }

    private async Task RunAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _catalogueStore
                .LoadCachedAsync(cancellationToken)
                .ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var result =
                    await _catalogueStore
                        .RefreshAsync(cancellationToken)
                        .ConfigureAwait(false);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Scheduled catalogue refresh failed: {Error}",
                        result.Error);
                }

                var hours =
                    Plugin.Instance?
                        .Configuration
                        .RefreshIntervalHours
                    ?? 6;

                await Task.Delay(
                    TimeSpan.FromHours(
                        Math.Clamp(hours, 1, 168)),
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Catalogue hosted service stopped unexpectedly.");
        }
    }
}

