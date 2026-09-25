# System Architecture Guide

This document provides a comprehensive deep dive into the design principles, core components, data flow, and limitations of the **Media Vault Jellyfin Plugin**.

---

## 🏗️ High-Level System Architecture

The plugin bridges Jellyfin with an external Media Vault catalogue and the **GPhotosDL** download daemon, enabling browsing and playback of cloud-hosted Google Photos media through standard Jellyfin client interfaces.

The cloud catalogue is exposed through Jellyfin Channels. When a user requests playback, the plugin asks GPhotosDL to obtain the corresponding media file, then uses Jellyfin's normal library resolution and media-source pipeline to make the real local file available for playback.

```text
                         ┌───────────────────┐
                         │    Media Vault    │
                         └─────────┬─────────┘
                                   │
                                   │ Catalogue JSON / API
                                   ▼
                         ┌───────────────────┐
                         │  CatalogueStore   │
                         │ (Last-Known-Good) │
                         └─────────┬─────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│  Movies Channel  │      │ TV Shows Channel │      │   F1 Channel     │
└────────┬─────────┘      └────────┬─────────┘      └────────┬─────────┘
         │                         │                         │
         └─────────────────────────┼─────────────────────────┘
                                   │
                                   ▼
                         ┌───────────────────┐
                         │  Jellyfin Server  │
                         └─────────┬─────────┘
                                   │
                                   │ User requests playback
                                   ▼
                         ┌───────────────────┐
                         │ Download / Playback│
                         │   Orchestration    │
                         └─────────┬─────────┘
                                   │
                                   │ HTTP API (/download)
                                   ▼
                         ┌───────────────────┐
                         │     GPhotosDL     │
                         └─────────┬─────────┘
                                   │
                                   │ Google Photos retrieval
                                   ▼
                         ┌───────────────────┐
                         │   Google Photos   │
                         └─────────┬─────────┘
                                   │
                                   │ Real local .mkv file
                                   ▼
                         ┌───────────────────┐
                         │ Targeted Jellyfin │
                         │  File Discovery    │
                         └─────────┬─────────┘
                                   │
                                   │ Real BaseItem
                                   ▼
                         ┌───────────────────┐
                         │ Jellyfin Media    │
                         │  Source Pipeline  │
                         └─────────┬─────────┘
                                   │
                                   │ Real MediaSourceInfo
                                   ▼
                         ┌───────────────────┐
                         │  Jellyfin Client  │
                         │     Playback      │
                         └───────────────────┘
```

The important distinction is that the plugin does **not** manufacture a fake Jellyfin media source for the cloud item. The downloaded file becomes a real local media file, Jellyfin resolves that file through its normal library/media pipeline, and the resulting real `MediaSourceInfo` is used for playback.

---

## 🎯 Core Design Principles

1. **Leverage Native Jellyfin Architecture:** Use supported Jellyfin Channel APIs rather than manipulating internal SQLite databases (`jellyfin.db`) or injecting dummy files.

2. **No "Fake" Media Placeholders:** Items in the cloud catalogue are rendered strictly through Channel items. Physical disk space is only consumed when a download is requested by a user.

3. **Non-Destructive Catalogue Resilience:** Network interruptions or corrupted updates must never destroy or wipe the local active catalogue.

4. **Decoupled Responsibilities:** Google Photos authentication, cookie handling, and browser automation remain strictly isolated inside GPhotosDL. The Jellyfin plugin only acts as an API client.

5. **Deterministic Universal Unique Identifiers (UUIDs):** Media Vault IDs map deterministically to fixed UUIDs so that playback history, watch states, and metadata remain stable across catalogue updates.

6. **Targeted File Discovery:** Minimises disk I/O during playback startup by targeting the expected local filename rather than triggering an unnecessary full-library scan.

---

## 📚 Catalogue & Data Persistence (`CatalogueStore`)

The plugin acts as a consumer of the Media Vault catalogue. It loads, validates, and transforms catalogue entries into internal DTOs.

A DTO (Data Transfer Object) is a lightweight, structured data model used specifically to transport catalogue data cleanly between the Media Vault JSON source, the plugin's internal memory, and Jellyfin's Channel interfaces without executing business logic.

### Catalogue Source Options

The catalogue JSON source can be supplied via:

* **Remote HTTP/HTTPS Endpoint:** `http://192.168.1.1:8080/api/jellyfin/catalogue`
* **Local Filesystem Path:** `/etc/mediavault/catalogue.json`

### Last-Known-Good Fallback Logic

To guarantee high availability, `CatalogueStore` implements atomic cached replacements.

```text
                  ┌─────────────────────────────────┐
                  │    Fetch Catalogue Source       │
                  └────────────────┬────────────────┘
                                   │
                                   ▼
                  ┌─────────────────────────────────┐
                  │      Read & Validate JSON       │
                  └────────────────┬────────────────┘
                                   │
                        ┌──────────┴──────────┐
                 SUCCESS│                     │FAILURE
                        ▼                     ▼
         ┌─────────────────────────┐   ┌──────────────────────────┐
         │ Write Temporary Cache   │   │ Keep Previous Valid      │
         └────────────┬────────────┘   │ Catalogue Active         │
                      │                └──────────────────────────┘
                      ▼
         ┌─────────────────────────┐
         │ Perform Atomic File     │
         │ Replacement (.tmp ──►)  │
         └────────────┬────────────┘
                      │
                      ▼
         ┌─────────────────────────┐
         │ Update Active Memory    │
         │ Catalogue               │
         └─────────────────────────┘
```

A failed catalogue refresh therefore does not replace the previously valid catalogue with invalid or incomplete data.

---

## 📺 Jellyfin Channel Architecture

The plugin registers three top-level Jellyfin Channels:

```text
Media Vault
├── 🎬 Movies
├── 📺 TV Shows
└── 🏎️ Formula 1
```

### 1. Movies Channel

Mapped as standard Channel-backed Movie items with comprehensive metadata:

* Title and Sort Title
* Plot Overview, Genres, and Age Ratings
* Release Date, Runtime, and IMDb/TMDb IDs
* High-Resolution Posters, Backdrops, and Cast Information
* Media Resolution, Codecs, File Sizes, and Media Vault ID

### 2. TV Shows Channel

Structured in a standard 3-tier hierarchy:

```text
TV Shows
└── Show Title
    └── Season
        └── Episode
```

Includes air dates, episode numbers, stills, runtimes, and Google Photos stream links.

### 3. Formula 1 Channel

Structured chronologically:

```text
Formula 1
└── Year Season (e.g., 2025 Season)
    └── Round (e.g., Round 01 - Silverstone)
        └── Event Type
```

Supported event types maintain deterministic sequence order:

1. `Sprint Qualifying`
2. `Sprint`
3. `Qualifying`
4. `Race`

---

## 📥 Download System & GPhotosDL Integration

The plugin proxies download commands to GPhotosDL so that Jellyfin clients never need direct access to the GPhotosDL server or port.

### Plugin Download API Endpoints

| Endpoint                               | Method   | Purpose                                                        |
| -------------------------------------- | -------- | -------------------------------------------------------------- |
| `/MediaVault/Downloads/start`          | `POST`   | Triggers a new media download job.                             |
| `/MediaVault/Downloads/catalogue`      | `GET`    | Fetches active catalogue items alongside download statuses.    |
| `/MediaVault/Downloads`                | `GET`    | Lists all active and pending download jobs.                    |
| `/MediaVault/Downloads/{mediaVaultId}` | `GET`    | Checks progress, speed, ETA, and cached runtime for an item.   |
| `/MediaVault/Downloads/{mediaVaultId}` | `DELETE` | Cancels an in-progress download or deletes local cached media. |

### GPhotosDL Interception & `.crdownload` Renaming

When Google Chrome downloads media, it creates temporary files named `xxxxxxxx.crdownload`. Jellyfin cannot identify growing media files with generic temporary extension names.

```text
Browser Download Stream
        │
        ▼
xxxxxxxx.crdownload
        │
        │ GPhotosDL interception
        ▼
Movie Name.mkv
        │
        │ Real file continues growing
        ▼
Targeted Jellyfin File Discovery
```

GPhotosDL immediately renames the stream to the expected target filename, such as:

```text
Back To The Future.mkv
```

This allows Jellyfin to recognise the file as the intended media file while GPhotosDL continues writing additional data to it.

### Live Download Progress Math

The web UI estimates playable media runtime during active downloads:

\(\text{Cached Runtime} = \text{Full Runtime} \times \frac{\text{Downloaded Bytes}}{\text{Total Bytes}}\)

This provides an estimate of how much of the expected runtime has been downloaded and cached locally.

---

## ▶️ Playback Resolution & Native Jellyfin Media Sources

The most important part of the playback architecture is that the cloud Channel item is not itself treated as a fake local media file.

The plugin uses the cloud catalogue item to determine **what** should be played and GPhotosDL to obtain the corresponding real local file. Jellyfin then resolves that local file through its normal media pipeline.

The process is:

```text
Cloud Channel item
        │
        ▼
Download requested
        │
        ▼
GPhotosDL writes real .mkv
        │
        ▼
Plugin resolves exact local path
        │
        ▼
Jellyfin resolves real BaseItem
        │
        ▼
Jellyfin produces real MediaSourceInfo
        │
        ▼
Client playback
```

### Step-by-step

1. **Cloud Channel item**

   The user selects a Movie, TV episode, or Formula 1 event from the Media Vault Channel.

2. **Download requested**

   The plugin determines the Media Vault item and its Google Photos source, then starts or reuses the corresponding GPhotosDL download job.

3. **GPhotosDL writes the real `.mkv`**

   GPhotosDL retrieves the media from Google Photos and writes it into the configured local download directory using the final Jellyfin-compatible filename.

4. **Plugin resolves the exact local path**

   The plugin knows the expected filename and configured download directory and targets that exact filesystem path rather than initiating a full Jellyfin library scan.

5. **Jellyfin resolves the real `BaseItem`**

   The local file is resolved through Jellyfin's normal library/item infrastructure. The plugin waits for Jellyfin to recognise the actual file where necessary.

6. **Jellyfin produces the real `MediaSourceInfo`**

   Once Jellyfin has resolved the local media item, the plugin uses the media source information produced by Jellyfin itself.

7. **Client playback**

   The Jellyfin client receives the normal Jellyfin media source and plays the real local file.

This approach is important because the plugin does **not** manufacture its own `MediaSourceInfo`, create a fake media file, or bypass Jellyfin's normal media-resolution pipeline.

### Why Targeted File Discovery Is Used

A conventional whole-library scan can be expensive on large libraries, particularly where media is stored on slower HDDs or network-backed storage.

Instead, the plugin targets the expected downloaded filename and uses Jellyfin's existing library-resolution mechanisms to discover the relevant item.

This keeps the playback path focused on the single media file being requested.

---

## 💾 Local Storage Directory Layout

Downloaded files are organised inside the configured target directories:

```text
Movies/
└── Movie Title (Year).mkv

TV Shows/
└── Show Name/
    └── Season 01/
        └── Show Name - S01E01 - Episode Title.mkv

Formula 1/
└── 2024/
    └── Round 01 - Bahrain/
        └── 05 - Race.mkv
```

Invalid path characters:

```text
:
?
*
/
\
"
<
>
|
```

are automatically sanitised to `_`.

The configured download directories should correspond to the appropriate local Jellyfin libraries so that Jellyfin can discover and resolve the downloaded files.

---

## 📱 Device Compatibility & Growing Files

Streaming a file while it is actively downloading requires client support for **growing media containers**.

The general playback path is:

```text
Media Vault Cloud Item
         │
         │ User requests playback
         ▼
      GPhotosDL
         │
         │ Downloads to local directory
         ▼
Real Growing .mkv File
         │
         │ Targeted File Discovery
         ▼
Jellyfin Local Media Item
         │
         │ Real MediaSourceInfo
         ▼
Jellyfin Client Playback
```

The file does not need to be completely downloaded before the plugin attempts to resolve it. The playback startup-buffer settings determine how long the plugin waits for enough data to become available before allowing playback to proceed.

### Hardware Workaround for Unresponsive Clients

Some clients may not reliably support playback from an actively growing media file.

Android TV is one example where this behaviour may occur.

In this situation:

1. Trigger the download from any device or the web dashboard.
2. Allow GPhotosDL to save the file into the local Jellyfin library directory.
3. Trigger a targeted discovery/scan of the corresponding local library if required.
4. Play the resulting item directly from the **Local Jellyfin Library** instead of the Cloud Channel view.

This avoids relying on growing-file playback support in clients that do not handle it reliably.

---

## ⚠️ Current Limitations & Future Roadmap

### Current Limitations

* **GPhotosDL Dependency:** Requires a separate running instance of GPhotosDL for Google Photos authentication and media retrieval.
* **In-Memory Tracking:** Active download tasks are managed in-memory by the plugin process.
* **Jellyfin Version Lock:** Built specifically against Jellyfin `10.11.11` API contracts.
* **JS Injector Requirement:** Enhanced frontend UI controls require separate setup of the Media Vault JavaScript through the JS Injector plugin.
* **Growing-File Client Compatibility:** Not every Jellyfin client reliably supports playback from a media file that is still being written to disk.
* **Local Library Requirement:** The configured download directory must be available to a corresponding Jellyfin local library so that Jellyfin can resolve the real downloaded media item.

### 🔮 Future Development Plan

* **Persistent Download State Tracking:** Enhance cache retention across server restarts so previously downloaded files are persistently recognised across all client devices without requiring manual local library discovery.
* **Multi-Node Download Cache Sharing:** Allow multiple Jellyfin instances to share a single centralised GPhotosDL cache repository.

---

## 📚 Related Documentation

* [Installation](INSTALLATION.md)
* [Configuration](CONFIGURATION.md)
* [Development](DEVELOPMENT.md)
* [Troubleshooting](TROUBLESHOOTING.md)
* [Channel Architecture](CHANNEL_ARCHITECTURE.md)

[Return to README](../README.md)
