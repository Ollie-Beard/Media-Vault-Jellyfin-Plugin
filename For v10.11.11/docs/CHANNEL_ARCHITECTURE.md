# Channel architecture — Jellyfin 10.11.11

`MediaVaultChannel : IChannel` is the user-facing adapter. It does not parse JSON, call Google Photos, or manufacture file paths.

```text
catalogue source -> CatalogueStore -> validated cached CloudMovieDto records
                                      |
                              MediaVaultChannel (IChannel)
                                      |
                         Cloud Movies folder -> ChannelItemInfo(Movie)
                                      |
                    Jellyfin ChannelManager -> persisted Jellyfin Movie item
```

The channel returns **Cloud Movies** at its root. Its child items have `Type = Media`, `ContentType = Movie`, `MediaType = Video`, empty `MediaSources`, and no media path. Jellyfin's ChannelManager owns public-API persistence and synchronisation. A removed Media Vault record is removed by the ChannelManager on the next Channel refresh; this plugin never writes `jellyfin.db` directly.

## Eligibility and identity

An item qualifies only when `savedToGooglePhotos` is true and `googlePhotosLink` is an absolute HTTP(S) URL. `downloadedToPC` is retained as a display tag, not cloud eligibility.

The identity is deterministic: SHA-1 over the plugin namespace UUID followed by the UTF-8 Media Vault `id`, with RFC 4122 version/variant bits applied. The derived GUID is rendered as `ChannelItemInfo.Id`; titles never affect identity.

## Metadata, refresh, and playback

The adapter maps title, overview, genres, release date/year, runtime, age rating, TMDb and Media Vault IDs, cast, director, community rating, collections (tags), state, resolution, medium, and TMDb poster URL. `ChannelItemInfo` has no sort-title or backdrop field, so Jellyfin derives sort order from the title and backdrop support is deliberately deferred. No TMDb request or bulk image download occurs during refresh.

`CatalogueHostedService` loads the last-known-good cache at startup and refreshes independently on the configured interval. Channel calls consume the in-memory cache only. The ChannelManager reconciles its persisted Channel items the next time it refreshes/enumerates that Channel; the importer itself never writes Jellyfin records. Playback is absent; a future `IRequiresMediaInfoCallback` will resolve the stable Media Vault ID, obtain a real cached file, then return `MediaSourceInfo`.
