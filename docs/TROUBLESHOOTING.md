# 🛠️ Media Vault — Troubleshooting Guide

This document provides a practical guide for diagnosing and fixing common **Media Vault Jellyfin Plugin** issues, including plugin visibility, catalogue failures, GPhotosDL connectivity, downloads, playback, targeted file discovery, and web UI problems.

The guide assumes **Jellyfin Server 10.11.11** and the current Media Vault plugin architecture.

---

## 🔎 1. Quick Troubleshooting Checklist

Before investigating a specific problem, check the following:

- Jellyfin Server is running **10.11.11**.
- Jellyfin has been restarted after installing or replacing the plugin DLL.
- The Media Vault plugin DLL is in the correct plugin directory.
- The configured catalogue source is reachable **from the Jellyfin server**.
- GPhotosDL is running, authenticated, and reachable from Jellyfin.
- The configured Movies, TV Shows, and Formula 1 download directories exist and are writable.
- The corresponding Jellyfin local libraries include those download directories.
- The required **File Transformation** and **JS Injector** plugins are installed if using the Media Vault web interface.
- The Media Vault JavaScript has been saved in JS Injector and Jellyfin has been refreshed afterwards.
- Jellyfin logs have been checked for Media Vault-related errors.

When troubleshooting, check the Jellyfin server logs as well as the client UI. Many Media Vault failures occur on the server side and therefore will not be obvious from the Jellyfin client.

---

## 🧩 2. Plugin Installation & Visibility

### Plugin does not appear after installation

First verify that the DLL exists at:

```text
<Jellyfin Data Directory>\plugins\MediaVault.Jellyfin\MediaVault.Jellyfin.dll
```

For example:

```text
Jellyfin\Data\plugins\MediaVault.Jellyfin\MediaVault.Jellyfin.dll
```

Make sure:

- Jellyfin was restarted after installation.
- The DLL is not inside another plugin folder.
- The DLL is readable by the Jellyfin process.
- The DLL was built for the Jellyfin version being used.
- The server is running **Jellyfin 10.11.11**.

If you built the plugin locally using the development workflow, the normal build output is:

```text
jellyfin-media-vault\src\MediaVault.Jellyfin\bin\Release\net9.0\MediaVault.Jellyfin.dll
```

Copy that DLL into the Jellyfin plugin directory described above. The same installation process is covered by steps 4 and 5 of `INSTALLATION.md`.

### Plugin appears but settings or functionality are missing

If the plugin appears in Jellyfin but expected settings or functionality are missing:

1. Open the Media Vault plugin configuration page.
2. Check that the catalogue source is configured.
3. Check the GPhotosDL URL.
4. Check the Movies, TV Shows, and Formula 1 download directories.
5. Check the playback startup buffer settings.
6. Save the configuration.
7. Restart Jellyfin if the change affects server-side services.

Then check the Jellyfin logs for Media Vault startup or configuration errors.

---

## 📚 3. Catalogue Problems

### Plugin appears but the catalogue is empty

Check the configured catalogue source.

Use the **Test Catalogue Connection** button in the plugin configuration page.

For an HTTP or HTTPS source, verify that the **Jellyfin server itself** can reach the URL.

For a local source, verify that the path exists on the **Jellyfin server**, not merely on the computer being used to administer Jellyfin.

Also check the Jellyfin logs for catalogue loading errors.

### Catalogue refresh fails

Check the Jellyfin logs for the Media Vault plugin.

Common causes include:

- Incorrect catalogue URL.
- Jellyfin cannot reach the catalogue server.
- Invalid JSON.
- Missing required movie data.
- Invalid or duplicate Media Vault IDs.
- Request timeout.
- File permissions.
- Incorrect local path.

The catalogue uses a last-known-good approach. A failed refresh should not destroy the previously successful catalogue.

If a refresh fails but the previous catalogue remains available, this is expected behaviour.

### Catalogue works once but later stops updating

Check:

- The catalogue source URL has not changed.
- The Jellyfin server can still reach the source.
- The source is returning valid JSON.
- The catalogue has not changed its expected structure.
- There are no duplicate or invalid Media Vault IDs.
- The configured refresh interval is appropriate.

If the source is intermittently unavailable, the plugin should retain the last successful catalogue rather than replacing it with an invalid or empty result.

---

## 🌐 4. GPhotosDL Connection & Download Problems

### GPhotosDL connection test fails

Check the configured **GPhotosDL URL** and verify that the Jellyfin server can reach the GPhotosDL HTTP API.

The plugin expects GPhotosDL to expose:

```text
POST /download
GET  /download/{id}
DELETE /download/{id}
```

Also verify that:

- GPhotosDL is running.
- GPhotosDL is authenticated.
- The configured URL points to the correct GPhotosDL instance.
- The Jellyfin server can reach that address and port.
- Any firewall or network rules allow the connection.

### Download does not start

Check:

- The catalogue item has a usable Google Photos link.
- GPhotosDL is reachable from Jellyfin.
- The configured download directory exists or can be created.
- Jellyfin has permission to write to the directory.
- The corresponding local Jellyfin library exists and points at the configured download directory.
- The item is not already locally available.
- GPhotosDL accepts the supplied Google Photos URL.

Also check the Jellyfin logs for a Media Vault download-start error and the GPhotosDL logs for the corresponding request.

### Download starts but progress does not update

Check that Jellyfin can continue communicating with GPhotosDL after the download has started.

The Media Vault plugin uses GPhotosDL as the source of truth for download progress.

Check:

- The GPhotosDL URL is still reachable.
- The download job still exists in GPhotosDL.
- GPhotosDL is returning the expected download status.
- There are no network interruptions between Jellyfin and GPhotosDL.
- The Jellyfin logs do not show repeated status-request failures.

If the download itself is progressing but the UI is not updating, investigate the Media Vault web interface separately.

### Download is cancelled unexpectedly

Check:

- Whether the download was manually cancelled.
- Whether GPhotosDL removed the job.
- Whether the Google Photos source became unavailable.
- Whether the download directory is writable.
- Whether there is enough disk space.
- Whether Jellyfin lost connectivity to GPhotosDL.

Check both the Jellyfin and GPhotosDL logs around the time the cancellation occurred.

---

## 💾 5. Jellyfin Cannot Find the Downloaded File

### The downloaded file exists but Jellyfin cannot find it

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
Media Vault Movies Download Directory ──► D:\MediaVault\Movies
Jellyfin Movies Library               ──► D:\MediaVault\Movies
```

If the Jellyfin library does not include the download directory, Jellyfin will not be able to identify the downloaded file as a normal local library item.

### The file is in the correct directory but playback still cannot find it

Check:

- The filename matches the expected Media Vault catalogue title.
- The file has the expected `.mkv` extension.
- The file is readable by Jellyfin.
- The relevant Jellyfin library includes the directory.
- Jellyfin has had an opportunity to discover the file.
- The Jellyfin item exists in the normal local library.

Media Vault deliberately uses **targeted file/item discovery** rather than triggering an unnecessary full Jellyfin-wide library scan.

This means troubleshooting should focus first on the expected local file path and the corresponding Jellyfin library.

### A locally cached file is not detected

If the media file already exists locally, Media Vault should treat it as cached and avoid starting another GPhotosDL download.

Check:

- The file exists in the configured download directory.
- The filename is correct.
- The extension is `.mkv`.
- The relevant Jellyfin local library includes the directory.
- Jellyfin can read the file.
- The corresponding Jellyfin library item has been discovered.

---

## ▶️ 6. Playback Problems

### Playback does not work while downloading

Some clients may not reliably support playback from an ever-growing media file. **Android TV is one example where this behaviour may occur.**

In this situation:

1. Start the download.
2. Allow the file to download to the appropriate local library directory.
3. Trigger a scan of the relevant local library only if the item has not already been discovered.
4. Play the resulting item from the normal local Jellyfin library.

This avoids relying on the cloud Channel's growing-file playback path for clients that do not handle growing files reliably.

### Playback works after the download finishes but not during the download

This usually indicates a client-side limitation with growing media files rather than a failure to download the media.

The Media Vault plugin uses the real local media file and Jellyfin's normal media pipeline. It does not create a fake `MediaSourceInfo` or fake media file.

If a particular client cannot reliably play the growing file:

- Allow the download to continue.
- Confirm the completed `.mkv` exists in the local library.
- Let Jellyfin discover the completed file.
- Start playback from the normal Jellyfin library.

### Playback fails even after the download completes

Check:

- The `.mkv` file exists.
- The file is readable by Jellyfin.
- The corresponding local Jellyfin library includes the directory.
- Jellyfin has discovered the file.
- The local Jellyfin item has a valid media source.
- The client can play the file format.

The Media Vault Channel is only responsible for resolving the catalogue item to the corresponding real local Jellyfin library item. Jellyfin remains responsible for the normal media-source and playback pipeline.

---

## ⏱️ 7. Playback Takes Too Long to Start

### Playback startup is slow

The current system targets the expected downloaded filename specifically instead of performing a full Jellyfin-wide library scan.

If playback is still slow, check:

- HDD performance.
- Jellyfin library size.
- Download speed.
- Startup buffer settings.
- Whether the local Jellyfin library is correctly configured.
- Whether the expected file already exists.
- Whether the client supports growing-file playback.
- Whether Jellyfin is spending time discovering or indexing the local item.

### The download is progressing but playback does not start

Check the configured playback startup buffer.

The plugin normally waits until enough of the local file is available to make Jellyfin's normal local-file playback path reliable, rather than waiting for the entire download to finish.

Also check:

- The file is increasing in size.
- The file is readable by Jellyfin.
- GPhotosDL is still reporting the download as active.
- Jellyfin can discover the expected local item.
- The client can play a growing file.

If the client does not support growing-file playback reliably, use the completed local-library playback workflow described above.

---

## 🖥️ 8. Media Vault Web Interface Problems

### Media Vault web interface does not appear

Check that both supporting plugins are installed:

- **File Transformation**
- **JS Injector**

Then verify that the Media Vault JavaScript has been installed in JS Injector using either:

```text
jellyfin-javascript-injector-scripts.json
```

or:

```text
Media_Vault.js
```

After saving the JS Injector configuration, refresh Jellyfin.

Also verify that the JS Injector plugin is compatible with the installed Jellyfin version.

### JavaScript Injector does not contain the Media Vault script

There are two supported installation methods:

- Import `jellyfin-javascript-injector-scripts.json`.
- Create a new script and paste the contents of `Media_Vault.js`.

Do not install both versions of the script at the same time unless there is a specific reason to do so.

After importing or pasting the script:

1. Click **Save**.
2. Refresh Jellyfin.
3. Re-open a Media Vault item detail page.

### The Media Vault panel appears but does not update

Check:

- The JavaScript is loaded only once.
- The Media Vault plugin API is reachable.
- The item has a Media Vault provider ID.
- A download has actually been started.
- The browser can reach the Jellyfin server.
- The browser console does not show JavaScript errors.

The web UI polls the Media Vault download status while a download is active.

If the server-side download status is correct but the panel is not updating, the problem is likely in the JavaScript injection layer rather than the GPhotosDL download itself.

### Download button is visible but does not work

Check:

- The Media Vault plugin is installed and enabled.
- The JavaScript is loaded.
- The item has a valid Media Vault ID.
- The GPhotosDL connection test succeeds.
- The browser can reach Jellyfin's Media Vault download endpoints.

The web UI relies on the Media Vault plugin's download API rather than implementing a separate download system.

### Web UI looks incorrect after an update

Try:

1. Save the JS Injector configuration again.
2. Refresh Jellyfin.
3. Perform a hard browser refresh.
4. Check that only one copy of the Media Vault script is installed.
5. Check the browser console for JavaScript errors.

If the problem only affects a particular Jellyfin client, test the same Media Vault item in Jellyfin Web to determine whether the issue is client-specific.

---

## 🎨 9. Visual or UI Glitches

### Buttons, panels, or controls appear in the wrong position

This is usually a JavaScript/CSS injection issue.

Check:

- The Media Vault script is installed correctly.
- The script is not being loaded twice.
- File Transformation and JS Injector are enabled.
- The Jellyfin client is running a compatible version.
- The browser console does not show JavaScript errors.

Refresh Jellyfin after making changes to the injected script.

### The Media Vault UI disappears after navigating around Jellyfin

The injected UI is attached to Jellyfin's dynamically rendered pages. Jellyfin may replace parts of the page while navigating between items.

Check:

- Whether the problem occurs only after navigating between detail pages.
- Whether a hard refresh restores the UI.
- Whether the browser console reports errors.
- Whether another custom Jellyfin script is modifying the same area of the page.

If the issue only occurs after navigation, inspect the JavaScript injection behaviour rather than the Media Vault backend.

### Focus or remote-control behaviour is incorrect on a TV client

If the Media Vault controls appear correctly but remote navigation repeatedly moves focus elsewhere, the issue may be caused by the Jellyfin client's focus management or by the injected UI being re-rendered.

Check:

- Whether the issue occurs only on the TV client.
- Whether it also occurs in Jellyfin Web.
- Whether the Media Vault panel is being rebuilt while a button has focus.
- Whether another injected script is modifying focus.
- Whether the issue occurs at regular polling intervals.

For Tizen-specific problems, test the same page and controls in Jellyfin Web where possible. This helps distinguish a general Media Vault UI problem from a client-specific focus-management issue.

---

## 🔤 10. Sort Order Problems

### Sort order is incorrect

The plugin includes a background **sort-title synchronisation** service.

Check that:

- The catalogue contains the expected `sortTitle`.
- The item belongs to the Media Vault Channel.
- The Media Vault provider ID is present.
- Jellyfin has completed Channel item creation or update.
- The item exists as the expected Jellyfin Movie item.

The synchronisation service intentionally targets existing Channel Movie items rather than performing a full library scan.

### Sort order does not update immediately

The synchronisation service runs in the background and reconciles Jellyfin's Channel-backed Movie items with the catalogue's expected sort title.

If a catalogue value has changed:

1. Confirm the new `sortTitle` is present in the catalogue.
2. Refresh the catalogue.
3. Allow Jellyfin to create or update the relevant Channel item.
4. Check the Jellyfin logs for sort-title synchronisation messages.

---

## 📝 11. Checking Logs

When a problem cannot be identified from the UI, check the Jellyfin server logs.

Search for terms such as:

```text
Media Vault
GPhotosDL
Catalogue
Download
MediaVaultChannel
SortTitle
```

Useful errors include:

- Catalogue connection failures.
- JSON deserialisation errors.
- GPhotosDL connection failures.
- Download-start failures.
- Download-status failures.
- Missing Google Photos links.
- Missing download directories.
- File permission errors.
- Failed local item discovery.
- Playback resolution failures.
- Sort-title synchronisation errors.

When reporting an issue, include:

- Jellyfin version.
- Media Vault plugin version.
- GPhotosDL version.
- The affected media type: Movie, TV Show, or Formula 1.
- Whether the problem occurs before, during, or after downloading.
- The relevant Jellyfin log entries.
- The relevant GPhotosDL log entries where applicable.
- The client being used.

Avoid posting Google Photos share URLs or other private information when sharing logs publicly.

---

## 🧭 12. Recommended Troubleshooting Order

When a Media Vault item is not working, troubleshoot from the outside in:

```text
Jellyfin Plugin
      │
      ▼
Catalogue
      │
      ▼
Google Photos Link
      │
      ▼
GPhotosDL Connection
      │
      ▼
Download Job
      │
      ▼
Local .mkv File
      │
      ▼
Jellyfin Local Library
      │
      ▼
Targeted Item Discovery
      │
      ▼
Real Jellyfin Media Source
      │
      ▼
Client Playback
```

This order helps identify which part of the pipeline is actually failing.

For example:

- If the catalogue is empty, investigate the catalogue before GPhotosDL.
- If the download never starts, investigate GPhotosDL before playback.
- If the `.mkv` exists but Jellyfin cannot play it, investigate the local library and item discovery.
- If playback works in Jellyfin Web but not on a particular TV client, investigate the client-specific behaviour.
- If the backend works but the download panel is broken, investigate the JavaScript injection layer.

---

## ⚠️ 13. Known Client Limitations

Media Vault ultimately relies on Jellyfin's normal playback pipeline and on the capabilities of the client being used.

In particular, clients may differ in how reliably they handle:

- Growing media files.
- Playback while a download is still in progress.
- Remote-control focus.
- Dynamically injected web UI.
- Client-specific Jellyfin Web behaviour.

If a client cannot reliably play a growing file, the recommended workaround is to allow the download to complete and then play the media from the normal local Jellyfin library.

The underlying Media Vault catalogue and download system can still be working correctly even when a particular client has a playback or UI limitation.

---

## 📚 Related Documentation

For the rest of the Media Vault project documentation:

- `INSTALLATION.md` — Complete installation and initial setup.
- `CONFIGURATION.md` — Plugin configuration and server settings.
- `ARCHITECTURE.md` — Plugin architecture and playback design.
- `DEVELOPMENT.md` — Building, testing, publishing, and development workflow.
