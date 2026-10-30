using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaVault.Jellyfin.Catalogue;
using MediaVault.Jellyfin.Channels;
using Xunit;

namespace MediaVault.Jellyfin.Tests;

public sealed class ChannelItemFactoryTests
{
    private static readonly CloudMovieDto Movie = new(
        CatalogueStore.StableId("media-vault-42"),
        "media-vault-42",
        "Example Film",
        "Example Film",
        "2024-05-02",
        "15",
        ["Drama", "Mystery"],
        ["Example Collection"],
        "An overview.",
        123,
        987654,
        "https://image.tmdb.org/t/p/w500/poster.jpg",
        "https://image.tmdb.org/t/p/w1280/backdrop.jpg",
        ["Actor One", "Actor Two"],
        "Director One",
        7.6,
        true,
        "https://photos.app.goo.gl/example",
        12.5,
        "1080p",
        "Digital");

    [Fact]
    public void Movie_mapping_is_fileless_and_has_stable_identity()
    {
        var item = ChannelItemFactory.Movie(Movie);

        Assert.Equal(Movie.Id.ToString("N"), item.Id);
        Assert.Equal(ChannelItemType.Media, item.Type);
        Assert.Equal(ChannelMediaContentType.Movie, item.ContentType);
        Assert.Equal(ChannelMediaType.Video, item.MediaType);
        Assert.Empty(item.MediaSources);

        Assert.Equal("987654", item.ProviderIds["Tmdb"]);
        Assert.Equal("media-vault-42", item.ProviderIds["MediaVault"]);

        Assert.Equal(
            "https://image.tmdb.org/t/p/w500/poster.jpg",
            item.ImageUrl);

        Assert.Contains(
            "Collection: Example Collection",
            item.Tags);

        Assert.Contains(
            "Also downloaded locally",
            item.Tags);

        Assert.Contains(
            "Cloud available",
            item.Tags);

        Assert.Contains(
            "Resolution: 1080p",
            item.Tags);

        Assert.Contains(
            "Medium: Digital",
            item.Tags);

        Assert.Contains(
            "File size: 12.5 GB",
            item.Tags);

        Assert.Equal(3, item.People.Count);
    }

    [Fact]
    public void Cloud_eligibility_requires_id_and_title()
    {
        var eligible = new MediaVaultMovie
        {
            Id = "media-vault-42",
            Title = "Example Film"
        };

        Assert.True(
            CatalogueStore.IsCloudEligible(eligible));

        Assert.False(
            CatalogueStore.IsCloudEligible(
                new MediaVaultMovie
                {
                    Title = "Example Film"
                }));

        Assert.False(
            CatalogueStore.IsCloudEligible(
                new MediaVaultMovie
                {
                    Id = "media-vault-42"
                }));

        Assert.False(
            CatalogueStore.IsCloudEligible(
                new MediaVaultMovie()));
    }

    [Fact]
    public void Missing_optional_metadata_does_not_prevent_mapping()
    {
        var minimal = Movie with
        {
            TmdbId = null,
            PosterUrl = null,
            BackdropUrl = null,
            Cast = [],
            Director = null,
            Rating = null,
            GooglePhotosLink = null,
            FileSizeGb = null,
            Resolution = null,
            Medium = null
        };

        var item = ChannelItemFactory.Movie(minimal);

        Assert.Null(item.ImageUrl);
        Assert.Empty(item.People);
        Assert.DoesNotContain(
            "Tmdb",
            item.ProviderIds.Keys);

        Assert.DoesNotContain(
            "Cloud available",
            item.Tags);

        Assert.Contains(
            "No Google Photos link",
            item.Tags);
    }
}