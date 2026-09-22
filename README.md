# Media Vault for Jellyfin

A custom Jellyfin server plugin for **Media Vault**, providing Jellyfin with a cloud-backed catalogue of Movies, TV Shows and Formula 1 content, integrated with **GPhotosDL** for on-demand Google Photos downloads and native Jellyfin playback.

The plugin is designed around Jellyfin's supported **Channel** architecture. Catalogue metadata is exposed to Jellyfin without pretending cloud content is already a local media file. When playback/download is requested, the plugin coordinates with GPhotosDL to download the real media file locally, allowing Jellyfin to use its normal media pipeline.

## How to Install the Plugin

### Install from Jellyfin Catalogue
Go to dashboard

Go to Dashboard -> Plugins -> Manage Repositories -> "+ New Repository" <br>
The repository name can be anything <br>
For the repository url, use: <br> 
Click Add
Navigate back to Plugins and click "Available", look for "Media Vault Cloud Catalogue" in the plugin list.
Install the plugin and restart the jellyfin server.


---

## Compatibility

| Component       | Version                                                  |
| --------------- | -------------------------------------------------------- |
| Jellyfin Server | **10.11.11**                                             |
| .NET            | **9.0**                                                  |
| Plugin target   | `net9.0`                                                 |
| Media Vault     | Current Media Vault catalogue/API format                 |
| GPhotosDL       | HTTP API compatible with the plugin's download endpoints |

`Directory.Packages.props` is pinned to Jellyfin **10.11.11**.

This plugin should be treated as version-specific. Do not change the Jellyfin package version or target framework unless the plugin has been rebuilt and tested against the corresponding Jellyfin server version.

---

## What the Plugin Does

The plugin connects Jellyfin to an existing Media Vault installation.

At a high level:

```text
                         Media Vault
                             │
                             │ Catalogue JSON/API
                             ▼
                    ┌───────────────────┐
                    │   CatalogueStore  │
                    │                   │
                    │ Last-known-good   │
                    │ local cache       │
                    └─────────┬─────────┘
                              │
             ┌────────────────┼────────────────┐
             │                │                │
             ▼                ▼                ▼
      Movies Channel    TV Shows Channel   Formula 1 Channel
             │                │                │
             └────────────────┼────────────────┘
                              │
                              ▼
                         Jellyfin
                              │
                     User requests media
                              │
                              ▼
                    Media Vault Downloads
                              │
                              ▼
                         GPhotosDL
                              │
                              ▼
                       Google Photos
                              │
                              ▼
                      Local MKV file
                              │
                              ▼
                    Jellyfin native playback
```

The plugin does **not** perform Google authentication or directly download Google Photos media. Those responsibilities belong to GPhotosDL.

---

# Main Features

- Jellyfin Channel integration for:
  - **Movies**
  - **TV Shows**
  - **Formula 1**
- Local filesystem or HTTP(S) catalogue source.
- Last-known-good catalogue caching.
- Atomic catalogue cache updates.
- Catalogue refresh on startup and periodically in the background.
- Manual catalogue refresh from the plugin configuration page.
- Catalogue status and movie information endpoints.
- Media Vault metadata mapped into Jellyfin.
- TMDb metadata and artwork URLs.
- Cast and director information.
- Genres and collections.
- Release dates and age ratings.
- Runtime information.
- Resolution and storage metadata.
- Deterministic Jellyfin IDs derived from Media Vault IDs.
- GPhotosDL integration for on-demand downloads.
- Download progress, speed and ETA information.
- Download cancellation.
- Local-file detection for already downloaded media.
- Cached runtime estimation for partially downloaded media.
- Configurable playback startup buffering.
- Automatic Jellyfin sort-title synchronisation.
- Dedicated configuration UI.
- Unit tests for catalogue mapping and channel item creation.
- No direct writes to `jellyfin.db`.
- No fake placeholder media files.

---

# Architecture

## Catalogue

The plugin consumes the Media Vault catalogue rather than owning the underlying Media Vault database.

The catalogue can be supplied from:

- A local filesystem path.
- An HTTP URL.
- An HTTPS URL.

The source is loaded by `CatalogueStore`.

The catalogue is validated and mapped into the plugin's internal models before being exposed to Jellyfin.

The plugin maintains a local cache containing the most recently successful catalogue.

### Last-known-good behaviour

A failed catalogue refresh does **not** destroy the previous successful catalogue.

The refresh process is effectively:

```text
Catalogue source
      │
      ▼
Read JSON
      │
      ▼
Validate / map
      │
      ├── Failed ──────► Keep previous catalogue
      │
      ▼
Successful catalogue
      │
      ▼
Write temporary cache
      │
      ▼
Atomic replacement
      │
      ▼
New active catalogue
```

This means a temporary network failure, malformed response or unavailable Media Vault server does not immediately make the existing Jellyfin catalogue disappear.

---

# Jellyfin Channels

The plugin registers three Jellyfin Channels:

```text
Media Vault
├── Movies
├── TV Shows
└── Formula 1
```

The exact channel presentation is controlled by Jellyfin's Channel system and may therefore differ slightly between Jellyfin clients.

## Movies

Movies are exposed as Jellyfin Channel-backed Movie items.

The plugin maps Media Vault information such as:

- Title
- Sort title
- Overview
- Release date
- Runtime
- Age rating
- Genres
- Collections
- TMDb ID
- Poster
- Backdrop
- Cast
- Director
- Rating
- Resolution
- Medium
- File size
- Media Vault ID

## TV Shows

TV content is represented hierarchically:

```text
TV Shows
└── Show
    └── Season
        └── Episode
```

The catalogue contains information for:

- TV shows
- Seasons
- Episodes
- Episode numbers
- Air dates
- Runtime
- TMDb episode IDs
- Posters/stills
- Ratings
- Resolution
- Medium
- File sizes
- Google Photos links

## Formula 1

Formula 1 content is represented hierarchically:

```text
Formula 1
└── Season / Year
    └── Round
        └── Event
```

Events can include:

- Sprint Qualifying
- Sprint
- Qualifying
- Race

The plugin maintains deterministic ordering for known event types so that a normal race weekend is presented in the expected sequence.

---

# Jellyfin Channel Architecture

The plugin deliberately uses Jellyfin's supported Channel architecture rather than attempting to manufacture ordinary library files.

The plugin does **not**:

- Create fake media files.
- Insert arbitrary rows into `jellyfin.db`.
- Pretend a Google Photos item is already a local file.
- Directly manage Jellyfin's Channel database records.

Instead, the plugin supplies catalogue metadata and allows Jellyfin's Channel infrastructure to manage the resulting Channel-backed items.

The important distinction is:

```text
Media Vault catalogue
        │
        ▼
     Plugin
        │
        ▼
 Jellyfin Channel
        │
        ▼
Jellyfin-managed Channel Movie/TV items
```

For more detail, see:

- `docs/CHANNEL_ARCHITECTURE.md`
- `docs/NATIVE_LIBRARY_SPIKE.md`

---

# GPhotosDL Integration

The plugin does not contain Google Photos authentication or downloading logic.

Instead, it communicates with a separate **GPhotosDL** server through its HTTP API.

```text
Jellyfin Plugin
      │
      │ HTTP
      ▼
   GPhotosDL
      │
      ▼
 Google Photos
```

This separation keeps the Jellyfin plugin responsible for Jellyfin integration while GPhotosDL remains responsible for Google Photos access and downloading.

## Required GPhotosDL API

The plugin uses the following GPhotosDL endpoints:

```text
POST   /download
GET    /download/{id}
DELETE /download/{id}
```

The plugin sends the Google Photos URL, target filename and destination directory when starting a download.

GPhotosDL then provides the byte-level download status.

---

# Download System

The plugin exposes its own Jellyfin-side download API so that Jellyfin clients and the Media Vault JavaScript interface do not need direct access to the GPhotosDL server.

```text
Jellyfin Client
      │
      ▼
Media Vault Jellyfin Plugin
      │
      ▼
GPhotosDL
      │
      ▼
Google Photos
```

This is intentional.

A Jellyfin client only needs access to the Jellyfin server. It does **not** need to connect directly to GPhotosDL on port `8282`.

## Download endpoints

The plugin provides:

```text
POST   /MediaVault/Downloads/start
GET    /MediaVault/Downloads/catalogue
GET    /MediaVault/Downloads
GET    /MediaVault/Downloads/{mediaVaultId}
DELETE /MediaVault/Downloads/{mediaVaultId}
```

These endpoints provide functionality including:

- Starting a download.
- Viewing active downloads.
- Viewing locally cached files.
- Checking progress.
- Checking download speed.
- Checking ETA.
- Cancelling downloads.
- Determining cached runtime.
- Identifying the corresponding Media Vault item.

---

# Local Media Storage

The download location is configurable separately for:

- Movies
- TV Shows
- Formula 1

The plugin generates safe filesystem paths from catalogue metadata.

The general layout is:

```text
Movies
└── Movie Title.mkv
```

```text
TV Shows
└── Show Name
    └── Season Name
        └── Episode.mkv
```

```text
Formula 1
└── Year
    └── Round
        └── Event Type.mkv
```

Invalid filename characters are replaced with `_` when necessary.

The actual configured root directories are controlled through the plugin configuration page.

---

# Local Jellyfin Libraries Are Required

The Media Vault Channel and the local Jellyfin library serve two different purposes.

The **cloud collection** provides the catalogue entry and initiates the download.

The **local Jellyfin library** provides the location where the real downloaded media file is discovered and played.

Therefore, every cloud collection must have a corresponding local Jellyfin library configured.

For example:

```text
Media Vault → Movies
        │
        ▼
Movies download directory
        │
        ▼
Local Jellyfin Movies library
```

The same principle applies to TV Shows and Formula 1.

For example:

```text
Media Vault → TV Shows
        │
        ▼
TV Shows download directory
        │
        ▼
Local Jellyfin TV Shows library
```

and:

```text
Media Vault → Formula 1
        │
        ▼
F1 download directory
        │
        ▼
Local Jellyfin F1 library
```

The Jellyfin local library must be configured to **probe the same location used as the corresponding download directory in the Media Vault plugin settings**.

If, for example, the plugin is configured to download Movies into:

```text
D:\MediaVault\Movies
```

then the Jellyfin Movies library needs to include:

```text
D:\MediaVault\Movies
```

as one of its library paths.

Without this local library:

1. GPhotosDL can successfully download the file.
2. The file can exist on disk.
3. Jellyfin can still be unable to identify the file as a Movie, Episode or Formula 1 event.
4. Playback through the normal local Jellyfin media pipeline cannot begin.

**A corresponding local Jellyfin library is therefore a required part of the system.**

---

# GPhotosDL Filename Handling

When GPhotosDL downloads media, Google Chrome initially creates a temporary download file with a name similar to:

```text
xxxxxxxx.crdownload
```

A `.crdownload` file is a browser download temporary file and does not contain the final media filename that Jellyfin expects.

GPhotosDL therefore intercepts the download and changes the filename to the intended media filename, for example:

```text
Back To The Future.mkv
```

rather than leaving it as:

```text
xxxxxxxx.crdownload
```

This is important because Jellyfin's local library needs to be able to identify the growing file as the actual piece of media being downloaded.

For example:

```text
xxxxxxxx.crdownload
```

does not provide Jellyfin with enough filename information to identify the file as **Back To The Future**.

Whereas:

```text
Back To The Future.mkv
```

allows Jellyfin's normal library scanning and media identification process to recognise the file correctly while it continues to grow.

This filename handling is performed by **GPhotosDL**, not by the Jellyfin plugin.

---

# Playback Startup Buffer

Google Photos downloads can take time to complete.

The plugin therefore supports starting playback while a file is still downloading, provided enough of the file has already been downloaded.

The configurable startup settings are:

- Maximum startup wait time.
- Startup buffer percentage.
- Minimum startup buffer in GB.
- Maximum startup buffer in GB.

The basic process is:

```text
Playback requested
       │
       ▼
Start Google Photos download
       │
       ▼
GPhotosDL downloads and names the file
       │
       ▼
Wait for sufficient local data
       │
       ▼
Jellyfin discovers the real MKV
       │
       ▼
Jellyfin native playback
```

The purpose of the startup buffer is to give Jellyfin/FFmpeg enough data at the beginning of the file before playback begins, reducing the chance of the playback pipeline reaching the end of a still-growing file immediately.

The plugin does not manufacture a fake `MediaSourceInfo` or fake media file to accomplish this.

---

# Targeted Library Refresh

The system originally used a Jellyfin-wide library rescan after a download was started.

While this worked, it introduced unnecessary delay because Jellyfin had to inspect the entire configured library.

This could be particularly noticeable on:

- Older hardware.
- Systems using mechanical HDDs.
- Servers with very large existing libraries.
- Systems where the Jellyfin library contains thousands of other files.

The current system avoids this full-library scan.

Instead, once a download is initiated, Jellyfin is instructed to look specifically for the expected downloaded filename.

For example:

```text
Back To The Future.mkv
```

rather than asking Jellyfin to rescan the entire Movies library.

Conceptually:

```text
Old behaviour:

Download starts
      │
      ▼
Full Jellyfin library scan
      │
      ▼
Eventually discovers file
      │
      ▼
Playback


Current behaviour:

Download starts
      │
      ▼
Look specifically for:
"Back To The Future.mkv"
      │
      ▼
Jellyfin identifies the file
      │
      ▼
Playback
```

This significantly reduces the amount of unnecessary filesystem work required before playback can begin and is particularly useful on slower hardware or large HDD-based libraries.

---

# Device Compatibility and Growing Files

Playing a file while it is still being downloaded relies on the Jellyfin client and its playback pipeline correctly handling an **ever-growing media file**.

Not every Jellyfin client/device behaves the same way.

For example, some devices such as **Android TV** may not reliably handle playback of a file that is still growing while it is being downloaded.

In these cases, the download itself can still be performed successfully.

A practical workaround is:

1. Initiate the download from the TV or another device.
2. Allow GPhotosDL to download the media into the configured local library directory.
3. Trigger a scan of the **specific corresponding local library**:
   - Movies
   - TV Shows
   - Formula 1
4. Once Jellyfin has identified the downloading/local file, play it from the normal local library.

This means the media is still downloading through the Media Vault/GPhotosDL system, but playback takes place from the local Jellyfin library rather than from the cloud collection populated by the plugin.

For example:

```text
Media Vault Cloud Collection
        │
        │ Download
        ▼
GPhotosDL
        │
        ▼
Local Movies Library
        │
        │ Specific library scan
        ▼
Jellyfin identifies file
        │
        ▼
Local Library Playback
```

---

# Download Progress

The plugin exposes GPhotosDL's download information including:

- Downloaded bytes
- Total bytes
- Percentage
- Download speed
- Speed in MB/s
- Estimated time remaining
- Error information
- Runtime
- Cached runtime
- Cached runtime percentage

For an active download, cached runtime is estimated from:

```text
Runtime × downloaded bytes / total bytes
```

For a completed/cached file, cached runtime is treated as the full runtime.

This information is used by the Media Vault Jellyfin web UI to present download progress to users.

---

# Sort Title Synchronisation

Media Vault has its own `sortTitle` values.

Jellyfin Channel Movies can maintain their own sort names, so the plugin includes `MediaVaultSortTitleHostedService` to keep the Jellyfin Channel items aligned with the Media Vault catalogue.

The service:

1. Finds the Media Vault Channel.
2. Finds existing Channel-backed Movie items.
3. Identifies the corresponding Media Vault ID.
4. Looks up the catalogue record.
5. Applies the Media Vault `sortTitle` when necessary.

This is performed against existing Jellyfin items rather than by directly modifying `jellyfin.db`.

The service also listens for relevant Jellyfin item events so new or updated Channel Movies can be reconciled.

---

# Configuration

Open:

**Dashboard → Plugins → Media Vault Cloud Catalogue**

The configuration page provides the following settings.

## Catalogue

### Catalogue Source

The source of the Media Vault catalogue.

Examples:

```text
http://192.168.1.1:xxxx/api/jellyfin/catalogue
```

or a server-local file path.

The plugin supports HTTP and HTTPS URLs as well as local filesystem paths.

### Request Timeout

Controls how long catalogue/GPhotosDL HTTP operations are allowed to wait.

---

## GPhotosDL

### GPhotosDL URL

The base URL of the GPhotosDL server.

For example:

```text
http://192.168.1.1:8282
```

The Jellyfin server must be able to reach this service.

The Jellyfin client does not need direct access to this address.

The configuration page also provides a connection test.

---

## Download Directories

Separate directories can be configured for:

- Movies
- TV Shows
- Formula 1

These directories must be writable by the Jellyfin server process.

**Each directory must also correspond to the appropriate Jellyfin local library.**

For example:

```text
Plugin Movies Directory
        ↓
Jellyfin Movies Library Path
```

```text
Plugin TV Shows Directory
        ↓
Jellyfin TV Shows Library Path
```

```text
Plugin F1 Directory
        ↓
Jellyfin F1 Library Path
```

---

## Catalogue Refresh

### Automatic Refresh Interval

Controls how frequently the plugin refreshes the catalogue automatically.

The supported range is:

```text
1–168 hours
```

The default is:

```text
6 hours
```

### Request Timeout

Controls catalogue HTTP request timeout.

Default:

```text
30 seconds
```

### Library Refresh Delay

Controls the delay associated with refreshing Jellyfin after a download has started.

Default:

```text
10 seconds
```

---

## Playback Startup Buffer

### Maximum Startup Wait

Maximum amount of time to wait for enough of a file to become available before playback starts.

Default:

```text
120 seconds
```

### Startup Buffer Percentage

Percentage of the expected file size to target before starting playback.

Default:

```text
5%
```

### Minimum Startup Buffer

Minimum amount of data to buffer before playback.

Default:

```text
0.5 GB
```

### Maximum Startup Buffer

Maximum amount of data to buffer before playback.

Default:

```text
3 GB
```

These values can be adjusted depending on download speed, file sizes and the playback devices being used.

---

# Installation

There are two intended installation methods:

1. Jellyfin Plugin Repository / Manifest
2. Manual DLL installation

## Method 1 — Plugin Repository

Once the plugin manifest has been published:

1. Open Jellyfin.
2. Go to **Dashboard → Plugins**.
3. Open **Manage Repositories**.
4. Add the Media Vault repository.
5. Enter the manifest URL shown at the top of this README.
6. Save the repository.
7. Find **Media Vault** in the plugin catalogue.
8. Install it.
9. Restart Jellyfin if requested.

The repository manifest allows Jellyfin to discover plugin releases and their associated binaries.

---

# Method 2 — Manual DLL Installation

Manual installation is useful for development builds or when installing a release before the repository manifest is available.

## 1. Stop Jellyfin

Stop the Jellyfin server before replacing or installing the plugin.

---

## 2. Locate the Jellyfin Data Directory

The plugin must be placed inside Jellyfin's `plugins` directory.

The general structure should be:

```text
<Jellyfin Data Directory>
└── plugins
    └── MediaVault
        └── MediaVault.Jellyfin.dll
```

If using a portable Jellyfin installation with a `Data` directory, this may look like:

```text
Jellyfin
└── Data
    └── plugins
        └── MediaVault
            └── MediaVault.Jellyfin.dll
```

On a Windows Jellyfin installation, the exact data directory depends on how Jellyfin was installed. Common locations include:

```text
C:\ProgramData\Jellyfin\Server\plugins
```

or:

```text
C:\Users\<YourUser>\AppData\Local\jellyfin\plugins
```

Jellyfin's current documentation lists `%ProgramData%\Jellyfin\Server\plugins` for tray installations and `%UserProfile%\AppData\Local\jellyfin\plugins` for direct installations.

The important part is that the plugin DLL goes into the server's:

```text
plugins\MediaVault\
```

directory.

---

## 3. Create a Dedicated Plugin Folder

Do **not** simply place the DLL loose inside the main `plugins` directory.

Create:

```text
plugins
└── MediaVault
```

Then place:

```text
MediaVault.Jellyfin.dll
```

inside it.

Final structure:

```text
plugins
└── MediaVault
    └── MediaVault.Jellyfin.dll
```

Do not put the DLL inside another plugin's directory.

---

## 4. Start Jellyfin

Start Jellyfin again.

The plugin should now appear under:

**Dashboard → Plugins → Installed**

Open:

**Media Vault Cloud Catalogue**

and configure the catalogue and GPhotosDL settings.

---

# Building From Source

## Requirements

Install:

- .NET 9 SDK
- Git
- A Jellyfin 10.11.11 development/test environment if testing against a live server

The repository contains:

```text
MediaVault.Jellyfin.sln
Directory.Packages.props
build.yaml
```

---

## Restore

From the repository root:

```powershell
dotnet restore
```

---

## Run Tests

```powershell
dotnet test .\MediaVault.Jellyfin.sln
```

The test project currently covers areas including:

- Catalogue mapping
- Channel item creation

---

## Publish the Plugin

Build the release plugin:

```powershell
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
```

The resulting plugin DLL will be placed in:

```text
artifacts\plugin\MediaVault.Jellyfin.dll
```

---

## Create a Manual Installation ZIP

The repository's existing packaging process creates a ZIP containing the plugin DLL:

```powershell
Compress-Archive `
    -Path .\artifacts\plugin\MediaVault.Jellyfin.dll `
    -DestinationPath .\artifacts\MediaVault.Jellyfin.zip `
    -Force
```

The ZIP can then be attached to a GitHub Release.

---

# Release Packaging

Generated build output should **not** be committed to the source tree.

Release binaries belong in GitHub Releases.

The source repository should contain the source and build configuration, while generated output such as:

```text
MediaVault.Jellyfin.dll
MediaVault.Jellyfin.pdb
MediaVault.Jellyfin.deps.json
MediaVault.Jellyfin.xml
```

should be generated during the build/release process.

A release should provide a package such as:

```text
MediaVault.Jellyfin.zip
```

containing:

```text
MediaVault.Jellyfin.dll
```

The ZIP is the preferred release asset for repository-based installation, while the DLL inside it is the file required for the basic manual installation described above.

---

# Catalogue Data Model

The catalogue supports three main content groups.

## Movies

```text
MediaVaultExport
└── Movies
```

## TV Shows

```text
MediaVaultExport
└── TV Shows
    ├── Seasons
    └── Episodes
```

## Formula 1

```text
MediaVaultExport
└── Formula 1
    ├── Seasons
    ├── Rounds
    └── Events
```

The plugin converts these source models into internal cloud DTOs before exposing them through Jellyfin.

---

# Catalogue Refresh Behaviour

Catalogue refreshes can occur:

- During plugin startup.
- Automatically according to the configured refresh interval.
- Manually from the plugin configuration page.

A manual refresh is available through:

**Dashboard → Plugins → Media Vault Cloud Catalogue → Refresh catalogue now**

The plugin keeps catalogue processing isolated from Jellyfin's normal startup path.

A failed refresh is recorded as an error while retaining the previous valid catalogue.

---

# Data Persistence

The plugin maintains its own cached catalogue in its plugin data directory.

The cache is written atomically using a temporary file before replacement.

This protects the last-known-good catalogue from being destroyed by an interrupted or failed write.

The cache also supports loading older catalogue cache formats where applicable.

Plugin configuration itself is handled through Jellyfin's plugin configuration system.

---

# Security and Network Requirements

The plugin does not require Jellyfin clients to have direct access to the GPhotosDL server.

The expected network relationship is:

```text
Jellyfin Client
      │
      │ Jellyfin connection
      ▼
Jellyfin Server
      │
      │ GPhotosDL HTTP API
      ▼
GPhotosDL
      │
      ▼
Google Photos
```

The Jellyfin server therefore needs network access to:

- The Media Vault catalogue source.
- The GPhotosDL server.
- Google Photos indirectly through GPhotosDL.

The Jellyfin client only needs its normal connection to Jellyfin.

---

# Troubleshooting

## Plugin does not appear after installation

Check:

```text
<Jellyfin Data Directory>\plugins\MediaVault\MediaVault.Jellyfin.dll
```

Make sure:

- Jellyfin was restarted after installation.
- The DLL is not inside another plugin folder.
- The DLL is readable by the Jellyfin process.
- The DLL was built for the Jellyfin version being used.
- The server is running Jellyfin 10.11.11.

---

## Plugin appears but the catalogue is empty

Check the configured catalogue source.

Use the **Test Catalogue Connection** button in the plugin configuration page.

For an HTTP source, verify that the Jellyfin server itself can reach the URL.

For a local source, verify that the path exists **on the Jellyfin server**, not merely on the computer used to administer Jellyfin.

---

## Catalogue refresh fails

Check the Jellyfin logs for the Media Vault plugin.

Common causes include:

- Incorrect catalogue URL.
- Jellyfin cannot reach the catalogue server.
- Invalid JSON.
- Missing `movies` data where required.
- Invalid or duplicate Media Vault IDs.
- Request timeout.
- File permissions.
- Incorrect local path.

The previous successful catalogue should remain available after a failed refresh.

---

## GPhotosDL connection test fails

Check:

```text
GPhotosDL URL
```

and verify that the Jellyfin server can reach the GPhotosDL HTTP API.

The plugin expects GPhotosDL's API to expose:

```text
POST /download
GET /download/{id}
DELETE /download/{id}
```

Also verify that GPhotosDL itself is running and authenticated.

---

## Download does not start

Check:

1. The catalogue item has a usable Google Photos link.
2. GPhotosDL is reachable from Jellyfin.
3. The configured download directory exists or can be created.
4. Jellyfin has permission to write to the directory.
5. The corresponding local Jellyfin library exists and points at the configured download directory.
6. The item is not already locally available.
7. GPhotosDL accepts the supplied Google Photos URL.

---

## Jellyfin cannot find the downloaded file

First check that GPhotosDL has renamed the temporary browser download from:

```text
xxxxxxxx.crdownload
```

to the intended filename, for example:

```text
Back To The Future.mkv
```

Then verify that the corresponding local Jellyfin library is configured to include the same directory used by the Media Vault plugin.

For example:

```text
Media Vault Movies Download Directory
        │
        ▼
D:\MediaVault\Movies
        │
        ▼
Jellyfin Movies Library
        │
        ▼
D:\MediaVault\Movies
```

If the Jellyfin library does not include the download directory, Jellyfin will not be able to identify the downloaded file.

---

## Playback does not work while downloading

Some clients may not reliably support playback from an ever-growing media file.

Android TV is one example where this behaviour may occur.

In this situation:

1. Start the download.
2. Allow the file to download to the appropriate local library directory.
3. Trigger a scan of the relevant local library only.
4. Play the resulting item from the local Jellyfin library.

This avoids relying on the cloud Channel's playback path for clients that do not handle growing files reliably.

---

## Playback takes too long to start

The system previously used a full Jellyfin-wide library scan after initiating a download.

The current system instead targets the expected downloaded filename specifically.

If playback is still slow, check:

- HDD performance.
- Jellyfin library size.
- Download speed.
- Startup buffer settings.
- Whether the local library is correctly configured.
- Whether the client supports growing-file playback.

---

## Sort order is incorrect

The plugin includes a background sort-title synchronisation service.

Check that:

- The catalogue contains the expected `sortTitle`.
- The item belongs to the Media Vault Channel.
- The Media Vault provider ID is present.
- Jellyfin has completed Channel item creation/update.

The synchronisation service intentionally targets existing Channel Movie items rather than performing a full library scan.

---

# Development Notes

The plugin is intentionally split into separate responsibilities.

```text
CatalogueStore
    │
    ├── Reads and validates catalogue
    ├── Maps source models
    └── Maintains cache
          │
          ▼
Channels
    │
    ├── Movies
    ├── TV Shows
    └── Formula 1
          │
          ▼
DownloadController
    │
    ├── Start
    ├── Status
    ├── Cancel
    └── Local-file detection
          │
          ▼
GPhotosDlService
          │
          ▼
      GPhotosDL
```

This keeps the plugin's Jellyfin integration separate from Google Photos authentication/download functionality.

---

# Design Principles

## Use Jellyfin's public architecture

The plugin uses Jellyfin's Channel APIs rather than directly manipulating the Jellyfin database.

## Do not fake local media

Cloud catalogue items are not represented as fake local files.

Actual local media exists as a real MKV file downloaded by GPhotosDL.

## Keep catalogue failures non-destructive

A failed refresh must not destroy the last valid catalogue.

## Keep GPhotosDL separate

Google Photos authentication and downloading belong to GPhotosDL.

The Jellyfin plugin only needs to communicate with its HTTP API.

## Use deterministic identities

Media Vault IDs are converted into deterministic UUID-style identifiers so that the same catalogue record can be consistently identified across refreshes.

## Keep local library discovery targeted

The system avoids unnecessary full-library rescans where possible by targeting the expected downloaded filename.

This reduces filesystem work and improves startup time on slower hardware and large libraries.

---

# Current Limitations

The following are current limitations or environmental considerations:

- The plugin does not implement Google Photos authentication itself.
- GPhotosDL is required for Google Photos downloads.
- Download jobs are tracked in memory by the Jellyfin plugin process.
- Jellyfin client Channel presentation can vary between clients.
- Some Jellyfin clients/devices may not reliably play an ever-growing media file.
- The plugin depends on Jellyfin 10.11.11 APIs and package versions.
- A corresponding local Jellyfin library is required for each configured download location.
- The plugin does not directly modify `jellyfin.db`.
- The plugin does not provide an independent cloud-hosted catalogue service.

---

# Future Development

Potential future work includes:

- Improved persistent/local cache handling across devices and Jellyfin server restarts.
- Improved detection and reuse of recently downloaded files across separate Jellyfin clients.
- Preventing a recently downloaded item from being unnecessarily deleted and downloaded again when the Jellyfin server restarts or the same item is requested from another device.

### Download/cache persistence

There is currently an important limitation around recently downloaded files.

If an item has been downloaded on one device, pressing Play again on that same running Jellyfin server can make use of the local downloaded file rather than immediately starting another download.

However, after a Jellyfin server restart, or when the same item is requested from a different device/server instance, the recently downloaded file may not be retained/reused as intended. In those situations, the existing file can be deleted and another Google Photos download can be started.

This is not ideal because the media has already been downloaded and should not need to be downloaded again.

To get around this limitation, initiate playback of a file from the local library, not the cloud collection.

Improving this behaviour so that downloaded media is persistently recognised and reused across server restarts and different clients/devices is a planned area of future development.

---

# Documentation

Additional technical documentation is available in:

- `docs/CHANNEL_ARCHITECTURE.md`
- `docs/NATIVE_LIBRARY_SPIKE.md`

These documents explain the reasoning behind the Channel architecture and the investigation into Jellyfin's native library/media model.

---
