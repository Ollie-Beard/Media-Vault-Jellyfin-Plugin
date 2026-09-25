# System Architecture Guide

This document provides a comprehensive deep dive into the design principles, core components, data flow, and limitations of the **Media Vault Jellyfin Plugin**.

## 🏗️ High-Level System Architecture

The plugin bridges Jellyfin with an external Media Vault catalogue and the **GPhotosDL** download daemon, enabling seamless browsing and streaming of cloud-hosted Google Photos media directly within standard Jellyfin client interfaces.

```
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
                                   │ User requests media
                                   ▼
                         ┌───────────────────┐
                         │  Plugin Download  │
                         │    Orchestrator   │
                         └─────────┬─────────┘
                                   │
                                   │ HTTP API (/download)
                                   ▼
                         ┌───────────────────┐
                         │     GPhotosDL     │
                         └─────────┬─────────┘
                                   │
                                   │ Direct Media Retrieval
                                   ▼
                         ┌───────────────────┐
                         │   Google Photos   │
                         └─────────┬─────────┘
                                   │
                                   │ Streamed Local File (.mkv)
                                   ▼
                         ┌───────────────────┐
                         │ Jellyfin Native   │
                         │ Playback Engine   │
                         └───────────────────┘
```

---

## 🎯 Core Design Principles

1. **Leverage Native Jellyfin Architecture:** Use supported Jellyfin Channel APIs rather than manipulating internal SQLite databases (`jellyfin.db`) or injecting dummy files.
2. **No "Fake" Media Placeholders:** Items in the cloud catalogue are rendered strictly through Channel items. Physical disk space is only consumed when a download is requested by a user.
3. **Non-Destructive Catalogue Resilience:** Network interruptions or corrupted updates must never destroy or wipe the local active catalogue.
4. **Decoupled Responsibilities:** Google Photos authentication, cookie handling, and browser automation remain strictly isolated inside GPhotosDL. The Jellyfin plugin only acts as an API client.
5. **Deterministic Universal Unique Identifiers (UUIDs):** Media Vault IDs map deterministically to fixed UUIDs so that playback history, watch states, and metadata remain stable across catalogue updates.
6. **Targeted Library Item Discovery:** Minimizes disk I/O during playback startup by targeting exact filenames rather than triggering global library rescans.

---

## 📚 Catalogue & Data Persistence (`CatalogueStore`)

The plugin acts as a consumer of the Media Vault catalogue. It loads, validates, and transforms catalogue entries into internal DTOs.
* A DTO (Data Transfer Object) is a lightweight, structured data model used specifically to transport catalogue data cleanly between the Media Vault JSON source, the plugin's internal memory, and Jellyfin's Channel interfaces without executing any business logic.

### Catalogue Source Options
The catalogue JSON source can be supplied via:
* **Remote HTTP/HTTPS Endpoint:** `http://192.168.1.1:8080/api/jellyfin/catalogue`
* **Local Filesystem Path:** `/etc/mediavault/catalogue.json`

### Last-Known-Good Fallback Logic

To guarantee high availability, `CatalogueStore` implements atomic cached replacements:

```
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

---

## 📺 Jellyfin Channel Architecture

The plugin registers three top-level Jellyfin Channels:

```
Media Vault
├── 🎬 Movies
├── 📺 TV Shows
└── 🏎️ Formula 1
```

### 1. Movies Channel
Mapped as standard Channel-backed Movie items with comprehensive metadata:
* Title & Sort Title
* Plot Overview, Genres, & Age Ratings
* Release Date, Runtime, & IMDb/TMDb IDs
* High-Resolution Posters, Backdrops, & Cast Information
* Media Resolution, Codecs, File Sizes, & Media Vault ID

### 2. TV Shows Channel
Structured in a standard 3-tier hierarchy:
```
TV Shows
└── Show Title
    └── Season
        └── Episode
```
Includes air dates, episode numbers, stills, runtimes, and Google Photos stream links.

### 3. Formula 1 Channel
Structured chronologically:
```
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

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/MediaVault/Downloads/start` | `POST` | Triggers a new media download job. |
| `/MediaVault/Downloads/catalogue` | `GET` | Fetches active catalogue items alongside download statuses. |
| `/MediaVault/Downloads` | `GET` | Lists all active and pending download jobs. |
| `/MediaVault/Downloads/{mediaVaultId}` | `GET` | Checks progress, speed, ETA, and cached runtime for an item. |
| `/MediaVault/Downloads/{mediaVaultId}` | `DELETE` | Cancels an in-progress download or deletes local cached media. |

### GPhotosDL Interception & `.crdownload` Renaming

When Google Chrome downloads media, it creates temporary files named `xxxxxxxx.crdownload`. Jellyfin cannot identify growing media files with generic temp extension names.

```
Browser Download Stream ──► xxxxxxxx.crdownload (Temp)
                                   │
                                   ▼ GPhotosDL Interception
                          Back To The Future.mkv
                                   │
                                   ▼ Targeted Scan
                          Jellyfin Library Identifies File
```

GPhotosDL immediately renames the stream to the expected target path (`Movie Name.mkv`), allowing Jellyfin's media parser to parse the file container while it continues to write to disk.

### Live Download Progress Math

The web UI estimates playable media runtime during active downloads:

$$\text{Cached Runtime} = \text{Full Runtime} \times \frac{\text{Downloaded Bytes}}{\text{Total Bytes}}$$

---

## 💾 Local Storage Directory Layout

Downloaded files are organized inside the configured target directories:

```
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

*Invalid path characters (`:`, `?`, `*`, `/`, `\`, `"`, `<`, `>`, `|`) are automatically sanitized to `_`.*

---

## 📱 Device Compatibility & Growing Files

Streaming a file while it is actively downloading requires client support for **growing media containers**.

```
Media Vault Cloud Item
         │
         │ User Clicks Play
         ▼
     GPhotosDL (Downloads to local directory)
         │
         │ Targeted Scan
         ▼
Local Jellyfin Library
         │
         │ Direct File Playback
         ▼
Jellyfin Native Client Playback
```

### Hardware Workaround for Unresponsive Clients
Some clients (e.g., older Android TV builds, standalone web OS apps) freeze or fail when reading growing files.

**Recommended Workaround:**
1. Trigger the download from any device or the web dashboard.
2. Allow GPhotosDL to save the file into the local Jellyfin library directory.
3. Trigger a targeted scan on the corresponding local library (*Movies*, *TV Shows*, or *Formula 1*).
4. Play the completed or buffered item directly from your **Local Jellyfin Library** instead of the Cloud Channel view.

---

## ⚠️ Current Limitations & Future Roadmap

### Current Limitations
* **GPhotosDL Dependency:** Requires a separate running instance of GPhotosDL for Google Photos authentication.
* **In-Memory Tracking:** Active download tasks are managed in-memory by the plugin process.
* **Jellyfin Version Lock:** Built specifically against Jellyfin `10.11.11` API contracts.
* **JS Injector Requirement:** Enhanced frontend UI controls require separate setup of the Media Vault JavaScript snippet via the JS Injector plugin.

### 🔮 Future Development Plan
* **Persistent Download State Tracking:** Enhance cache retention across server restarts so previously downloaded files are persistently recognized across all client devices without requiring manual local library scans.
* **Multi-Node Download Cache Sharing:** Allow multiple Jellyfin instances to share a single centralized GPhotosDL cache repository.

Return to README
