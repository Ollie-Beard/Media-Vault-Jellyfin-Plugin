using MediaVault.Jellyfin.Catalogue;
using Xunit;

namespace MediaVault.Jellyfin.Tests;

public sealed class CatalogueMappingTests
{
    [Fact] public void Stable_id_is_deterministic_and_distinct() { Assert.Equal(CatalogueStore.StableId("movie-1"), CatalogueStore.StableId("movie-1")); Assert.NotEqual(CatalogueStore.StableId("movie-1"), CatalogueStore.StableId("movie-2")); }
    [Theory][InlineData("/poster.jpg", "https://image.tmdb.org/t/p/w500/poster.jpg")][InlineData(null, null)][InlineData("https://untrusted.example/a.jpg", null)]
    public void Artwork_requires_a_tmdb_relative_path(string? path, string? expected) => Assert.Equal(expected, CatalogueStore.TmdbImage(path, "w500"));
}
