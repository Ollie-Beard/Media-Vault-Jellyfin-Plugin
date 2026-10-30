# Configuration & Performance Tuning Guide

This document provides a detailed breakdown of all settings, parameters, and background synchronisation behavior in the **Media Vault Jellyfin Plugin**.

---

## ⚙️ Accessing Plugin Settings

To access the configuration dashboard:

1. Log into Jellyfin as an Administrator.
2. Navigate to **Dashboard** → **Plugins** (under the Admin section).
3. Select **Media Vault Cloud Catalogue**.

---

## 🌐 Basic Plugin Settings

### Catalogue Source

Controls where the plugin retrieves the Media Vault catalogue data.

* **Catalogue JSON URL or Local Path:** Specifies the HTTP(S) endpoint or server-local file system path containing the catalogue JSON file.
  * *Cloudflare Link:* `https://media-vault-cloud-catalogue.parkerjack931.workers.dev/`
  * *Local HTTP Example:* `http://192.168.1.1:3001/api/jellyfin/catalogue`
  * *Local File Example:* `C:\MediaVault\catalogue.json` or `/etc/mediavault/catalogue.json`
* **Request Timeout:** The maximum allowed wait time for catalogue HTTP operations.
  * **Default:** `30 seconds`

---

### GPhotosDL Service Integration

Configures communication between Jellyfin and the GPhotosDL download daemon.

* **GPhotosDL URL:** The base HTTP address of the GPhotosDL server.
  * *Example:* `http://192.168.1.1:8282` or `http://localhost:8282`
* **Starting GPhotosDL:** Connection will not be established if the program is not running. To run it use `.\gphotosdl.exe -show -debug` in the directory where the application is installed, you should see a line in the terminal that says `INFO HTTP server started address=http://localhost:8282`, use that link.
* **Connection Test:** Click the **Test Connection** button on the settings page to verify that the Jellyfin server process can reach the GPhotosDL endpoint.

> [!NOTE]
> The GPhotosDL service only needs to be accessible by the **Jellyfin Server**. Client devices (web browsers, smart TVs, mobile apps) do not need direct network access to GPhotosDL.

---

### Download Directories & Library Mapping

Specifies where downloaded media files are saved locally on the Jellyfin server.

* **Movies Directory:** Local path for cached movie files.
* **TV Shows Directory:** Local path for cached television series episodes.
* **Formula 1 Directory:** Local path for cached F1 events and motorsport media.

> [!IMPORTANT]
> **Directory Mapping Rules:**
> 1. All directories must have **read and write permissions** granted to the Jellyfin server process.
> 2. Each plugin directory **must match the exact path** configured in your corresponding Jellyfin local library.

```text
Plugin Movies Directory   ──► Jellyfin Movies Library Path
Plugin TV Shows Directory ──► Jellyfin TV Shows Library Path
Plugin F1 Directory       ──► Jellyfin F1 Library Path
```

---

## ⏱️ Catalogue Refresh Settings

Controls how and when the catalogue data is synchronised from the remote source.

| Setting | Description | Range / Default |
| :--- | :--- | :--- |
| **Automatic Refresh Interval** | Frequency at which the plugin background service pulls fresh catalogue updates. | **1–168 hours** *(Default: 6 hrs)* |
| **Request Timeout** | HTTP timeout limit when pulling catalogue updates. | **30 seconds** |
| **Library Refresh Delay** | Delay before Jellyfin initiates a targeted filesystem check after a download starts. | **10 seconds** |

### Refresh Modes & Error Handling
Catalogue refreshes occur automatically under three conditions:
1. **Plugin Startup:** Executed during Jellyfin server boot.
2. **Scheduled Background Interval:** Executed automatically based on the configured hourly interval.
3. **Manual Trigger:** Initiated on-demand via **Dashboard → Plugins → Media Vault Cloud Catalogue → Refresh catalogue now**.

> [!NOTE]
> Catalogue processing runs independently of Jellyfin's primary startup path. If a catalogue update fails due to network or server issues, an error is logged while the plugin gracefully falls back to the **last valid cached catalogue**.

---

## ⚡ Playback Startup Buffer Tuning

When a user requests playback of a cloud item, the plugin requests GPhotosDL to start downloading the file locally. To prevent player timeouts and stuttering, the plugin enforces a startup buffering window before handing the file over to Jellyfin's playback engine.

### Configuration Parameters

```text
               ┌─────────────────────────────────────────┐
               │         Download Initiated              │
               └────────────────────┬────────────────────┘
                                    │
                                    ▼
               ┌─────────────────────────────────────────┐
               │    Check Buffer Criteria Met?           │
               │    - Min Buffer reached? (e.g. 0.5GB)   │
               │    - Target % reached? (e.g. 5%)        │
               └──────────┬───────────────────┬──────────┘
                          │                   │
                  YES     │                   │ NO (Wait)
                          ▼                   ▼
            ┌───────────────────┐   ┌────────────────────┐
            │ Start Playback in │   │ Max Timeout Met?   │
            │     Jellyfin      │   │   (e.g. 120s)      │
            └───────────────────┘   └─────────┬──────────┘
                                              │
                                              ▼
                                    ┌────────────────────┐
                                    │ Force Start / Fail │
                                    └────────────────────┘
```

* **Startup Buffer Percentage:** The target percentage of the total file size that must download before initiating playback.
  * **Default:** `5%`
* **Minimum Startup Buffer:** The absolute minimum amount of data required before playback is permitted to launch.
  * **Default:** `0.5 GB`
* **Maximum Startup Buffer:** The maximum pre-buffered data cap. Once reached, playback starts immediately even if the percentage target is not met.
  * **Default:** `3 GB`
* **Maximum Startup Wait:** The absolute time limit the server will wait for the buffer threshold to be reached before attempting playback launch.
  * **Default:** `120 seconds`

---

## 🎯 Targeted Library Discovery Mechanics

Historically, initiating a media stream triggered a global, server-wide Jellyfin library scan to detect the newly downloading file. On systems with large libraries or spinning HDDs, this created significant performance bottlenecks.

The Media Vault plugin uses **Targeted Library Discovery** to eliminate full-library scans.

### Comparison Overview

#### Legacy Behavior (Full Scan)
```text
Download Starts ──► Full Jellyfin Library Scan ──► File Discovered (Slow) ──► Playback Begins
```

#### Current Behavior (Targeted Scan)
```text
Download Starts ──► Targeted File Query ("Back To The Future.mkv") ──► File Identified ──► Playback Begins
```

### Benefits of Targeted Item Discoveries
* **Reduced Disk I/O:** Prevents unnecessary disk reads across thousands of unrelated media files.
* **Faster Startup:** Drastically reduces total waiting time between clicking "Play" and video rendering.
* **Lower CPU Usage:** Eliminates metadata scraping overhead on low-power devices (Raspberry Pi, NAS devices, older servers).

---

## 🔄 Sort Title Synchronisation Service

Jellyfin Channel items can occasionally lose custom sort titles when metadata is updated or re-indexed. To maintain consistent ordering across Cloud Collections, the plugin runs a background service called `MediaVaultSortTitleHostedService`.

### How the Sync Service Operates

```text
 1. Locate Media Vault Channel
 2. Scan Channel-backed Movie items
 3. Map Jellyfin item to Media Vault ID
 4. Query Catalogue Record
 5. Apply Catalogue 'sortTitle' if mismatched
```

* **Direct API Alignment:** The service synchronises metadata directly via standard Jellyfin API abstractions rather than executing raw queries against `jellyfin.db`.
* **Event Listening:** Listens to Jellyfin item creation and modification events so newly added items are reconciled in real time.

---

## 🔒 Network Architecture & Security

Clients streaming from Jellyfin do not need access or credentials for the GPhotosDL server or Google Photos endpoints. All external API orchestration and file stream proxying are handled server-side by the Jellyfin host.

```text
┌───────────────────────┐
│    Jellyfin Client    │ (Web UI, Smart TV, Mobile App)
└───────────┬───────────┘
            │
            │ Standard Jellyfin Stream / API Connection
            ▼
┌───────────────────────┐
│    Jellyfin Server    │ (Media Vault Plugin Installed)
└───────────┬───────────┘
            │
            │ GPhotosDl HTTP API (Default Port 8282)
            ▼
┌───────────────────────┐
│    GPhotosDL Daemon   │ (Headless Browser Downloader)
└───────────┬───────────┘
            │
            │ Download Streams
            ▼
┌───────────────────────┐
│     Google Photos     │
└───────────────────────┘
```
## 📚 Related Documentation

* [Installation](INSTALLATION.md)
* [Development](DEVELOPMENT.md)
* [Architecture](ARCHITECTURE.md)
* [Troubleshooting](TROUBLESHOOTING.md)
* [Channel Architecture](CHANNEL_ARCHITECTURE.md)

[Return to README](../README.md)
