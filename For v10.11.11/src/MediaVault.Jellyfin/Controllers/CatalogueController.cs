using System.Text.Json;
using MediaVault.Jellyfin.Catalogue;
using MediaVault.Jellyfin.Configuration;
using MediaVault.Jellyfin.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaVault.Jellyfin.Controllers;

[ApiController]
[Route("MediaVault/Catalogue")]
public sealed class CatalogueController : ControllerBase
{
    private readonly CatalogueStore _catalogueStore;
    private readonly MediaVaultSortTitleHostedService _sortTitleHostedService;

    public CatalogueController(
        CatalogueStore catalogueStore,
        MediaVaultSortTitleHostedService sortTitleHostedService)
    {
        _catalogueStore = catalogueStore;
        _sortTitleHostedService = sortTitleHostedService;
    }

    [HttpGet("status")]
    public ActionResult<CatalogueStatus> Status() =>
        Ok(_catalogueStore.Status);

    [HttpGet("movies")]
    public ActionResult<IReadOnlyList<CloudMovieDto>> Movies() =>
        Ok(_catalogueStore.Movies);

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResult>> Refresh(
        CancellationToken cancellationToken)
    {
        var result = await _catalogueStore
            .RefreshAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            await _sortTitleHostedService
                .ReconcileExistingMoviesAsync()
                .ConfigureAwait(false);
        }

        return Ok(result);
    }
}