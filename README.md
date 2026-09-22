# Media Vault for Jellyfin

An isolated Jellyfin server-plugin project for Jellyfin **Server/Web 10.11.11**. It imports the existing Media Vault JSON export, preserves the last successful catalogue, and exposes eligible cloud films through Jellyfin's supported **Channel** architecture without pretending they are local files.

## Important compatibility gate

This package targets Jellyfin **10.11.11 / net9.0**. `Directory.Packages.props` is pinned to 10.11.11; do not change it unless the server is upgraded and compatibility is retested.

The Channel framework is Jellyfin's supported virtual-content model: Jellyfin owns the resulting Channel-backed `Movie` records while the plugin supplies file-less catalogue metadata. It does not create placeholder files or write `jellyfin.db` directly. See `docs/CHANNEL_ARCHITECTURE.md`.

## What is included

- Configurable local-path or HTTP(S) JSON source.
- Strict root validation; isolated malformed-record errors; duplicate-ID detection.
- Eligibility: `savedToGooglePhotos == true` plus an absolute `http`/`https` `googlePhotosLink`.
- Deterministic UUIDv5-style IDs derived from Media Vault record IDs.
- Last-known-good, atomic JSON cache; a failed refresh never clears it.
- Channel hierarchy: **Media Vault → Cloud Movies**, containing Movie-style records.
- Startup/periodic cached catalogue refresh plus manual refresh, status API and configuration page.
- Metadata mapping including TMDb artwork URLs, cast/director, collections and storage facts.
- Unit-test project (requires a .NET SDK to run).

## Build and package

Install the .NET 9 SDK, then from this directory:

```powershell
dotnet restore
dotnet test .\MediaVault.Jellyfin.sln
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
Compress-Archive -Path .\artifacts\plugin\MediaVault.Jellyfin.dll -DestinationPath .\artifacts\MediaVault.Jellyfin.zip -Force
```

`build.yaml` is provided for a repository/catalogue release workflow. For local development, stop Jellyfin, copy the published DLL into its dedicated plugins subdirectory, then start it again. Do not overwrite another plugin's folder.

## Configure and use

1. In Media Vault, export Movies with **all** of Titles, Metadata, Media Vault info and TMDb metadata selected.
2. Copy the export to a path readable by the Jellyfin server, or host it through HTTPS.
3. Install the DLL and restart Jellyfin.
4. Open Dashboard → Plugins → Media Vault Cloud Catalogue.
5. Enter the source path/URL and save.
6. Click **Refresh catalogue**, then open Channels → Media Vault → Cloud Movies.

The source export must remain outside this package; no original Media Vault file is copied or changed. Put a non-sensitive representative export in `tests/fixtures/` only when you choose to test locally.

## Current limitations

- No Google Photos download or playback.
- No TV import.
- Channel navigation and presentation should be tested on every client you use; it is known to vary more than ordinary libraries.
- Backdrop artwork and a custom in-client “Play from Cloud” action are not implemented.
- Normal Play is intentionally unavailable until a future deferred media-source/cache implementation exists.

## Existing application assessment

The untouched Media Vault application already has a browser-side v3.1 selective JSON export and server-side Google Photos/rclone endpoints. A future read-only `/api/jellyfin/catalogue` endpoint could be cleaner than a static export, but it is intentionally not added here because this delivery may only add files.
