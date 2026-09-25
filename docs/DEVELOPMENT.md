# 🛠️ Media Vault — Development & Build Guide

This document describes how to develop, test, build, package, and maintain the **Media Vault Jellyfin Plugin**.

It is intended for contributors and developers working on the C# Jellyfin plugin itself. Installation and runtime configuration are documented separately.

---

## 📚 1. Development Overview

The Media Vault Jellyfin Plugin is a .NET 9 Jellyfin plugin targeting **Jellyfin Server 10.11.11**.

The repository contains:

* The Jellyfin plugin source code.
* Automated unit tests.
* Jellyfin plugin metadata.
* Centralised NuGet package version management.
* Development and diagnostic helpers.
* Documentation.

The normal development cycle is:

```text
Edit Source
    │
    ▼
Restore Dependencies
    │
    ▼
Run Unit Tests
    │
    ▼
Publish Plugin
    │
    ▼
Test Against Jellyfin
    │
    ▼
Package Release
    │
    ▼
GitHub Release
```

The JavaScript Injector component is maintained separately from the C# plugin source and should therefore be treated as a separate development component.

For the runtime architecture of the plugin, see [`ARCHITECTURE.md`](./ARCHITECTURE.md).

---

# 🏗️ 2. Repository Structure

The repository is organised into source code, tests, build metadata, documentation, and generated output.

```text
Media-Vault-Jellyfin-Plugin/
│
├── artifacts/
│   └── plugin/
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── CHANNEL_ARCHITECTURE.md
│   ├── CONFIGURATION.md
│   ├── DEVELOPMENT.md
│   ├── INSTALLATION.md
│   ├── NATIVE_LIBRARY_SPIKE.md
│   └── TROUBLESHOOTING.md
│
├── src/
│   └── MediaVault.Jellyfin/
│       ├── Catalogue/
│       ├── Channels/
│       ├── Configuration/
│       ├── Controllers/
│       ├── Services/
│       ├── MediaVault.Jellyfin.csproj
│       └── Plugin.cs
│
├── tests/
│   └── MediaVault.Jellyfin.Tests/
│       ├── CatalogueMappingTests.cs
│       ├── ChannelItemFactoryTests.cs
│       └── MediaVault.Jellyfin.Tests.csproj
│
├── build.yaml
├── Directory.Packages.props
├── inspect-jellyfin.cs
├── MediaVault.Jellyfin.sln
├── README.md
└── restore-diag.txt
```

### Generated Directories and Files

The following are development or build outputs rather than source files:

```text
artifacts/
bin/
obj/
restore-diag.txt
```

These should not be treated as part of the plugin's source architecture.

Release binaries are distributed through GitHub Releases rather than committed into the source tree.

---

# 📦 3. Development Requirements

## .NET SDK

The plugin currently targets:

```text
.NET 9
Target Framework: net9.0
```

Install the .NET 9 SDK before attempting to restore, test, or publish the project.

---

## Git

Git is required for working with the repository and its source history.

---

## Jellyfin 10.11.11

The plugin is currently built against:

```text
Jellyfin Server 10.11.11
```

The Jellyfin package versions are pinned through:

```text
Directory.Packages.props
```

Development against a different Jellyfin version should not be assumed to be compatible.

If the Jellyfin dependency version or target framework is changed, the plugin should be rebuilt and tested against the corresponding Jellyfin server version before release.

---

## Optional Jellyfin Test Server

A local or otherwise accessible Jellyfin 10.11.11 server is useful for integration testing.

The automated test project does not require a running Jellyfin server for its normal unit tests.

---

# 🧩 4. Important Project Files

Several files in the repository have distinct roles.

Understanding the difference between them is important when modifying the project.

---

## `MediaVault.Jellyfin.sln`

The solution file contains the projects that make up the development solution.

It currently includes:

```text
MediaVault.Jellyfin
MediaVault.Jellyfin.Tests
```

The solution is useful for:

* Restoring the complete solution.
* Running the full test suite.
* Opening the project in an IDE.
* Building the plugin and tests together.

---

## `src/MediaVault.Jellyfin/MediaVault.Jellyfin.csproj`

This is the **actual Jellyfin plugin project**.

It defines the plugin's .NET project configuration and is the project used when publishing the plugin.

The normal release publish command targets this file directly:

```powershell
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
```

---

## `tests/MediaVault.Jellyfin.Tests/MediaVault.Jellyfin.Tests.csproj`

This is the automated test project.

It contains unit tests for plugin logic that can be tested independently of a running Jellyfin server.

Current tests include areas such as:

* Catalogue mapping.
* Channel item generation.

---

## `Directory.Packages.props`

This file provides central NuGet package version management.

The Jellyfin dependencies are pinned to the currently supported Jellyfin version:

```text
10.11.11
```

Keeping these versions centralised helps prevent individual projects from accidentally using inconsistent package versions.

---

## `build.yaml`

`build.yaml` is **not a CI/CD workflow**.

It contains Jellyfin plugin/build metadata used to describe the plugin, including information such as:

* Plugin name.
* Plugin GUID.
* Version.
* Target ABI.
* Target framework.
* Overview.
* Description.
* Category.
* Owner.
* Build artifacts.

The file should therefore be treated as **Jellyfin plugin metadata**, rather than as a GitHub Actions or general CI/CD configuration file.

---

## `inspect-jellyfin.cs`

`inspect-jellyfin.cs` is a development/diagnostic helper used to inspect Jellyfin API types and members.

It is useful when investigating Jellyfin internals or determining how a particular Jellyfin API type is exposed.

It is **not part of the normal plugin build path**.

---

# 🔄 5. Restore Dependencies

Run the following from the repository root:

```powershell
dotnet restore
```

This restores the NuGet dependencies required by the solution.

The repository root should be the current working directory when running the command:

```text
Media-Vault-Jellyfin-Plugin/
```

After a successful restore, the solution is ready for testing or publishing.

---

# 🧪 6. Running the Automated Tests

The complete automated test suite can be run with:

```powershell
dotnet test .\MediaVault.Jellyfin.sln
```

This builds the relevant projects and executes the tests contained in:

```text
tests/MediaVault.Jellyfin.Tests/
```

---

## Current Test Coverage

The existing tests primarily cover deterministic plugin logic rather than end-to-end Jellyfin playback.

Examples include:

### Catalogue Mapping

Tests verify that catalogue data is correctly mapped into the plugin's internal models.

This helps protect against regressions in:

* Catalogue deserialisation.
* DTO/model mapping.
* Media Vault metadata handling.

### Channel Item Creation

Tests verify the generation of Jellyfin Channel folders and items.

This helps protect the structure exposed by:

* Movies.
* TV Shows.
* Formula 1.

---

## Test Philosophy

The automated test suite should focus on logic that can be tested deterministically without requiring a running Jellyfin server or Google Photos connection.

Live integration behaviour can then be tested separately against a Jellyfin development environment.

---

# 🚀 7. Publishing the Plugin

The plugin can be published using the `Release` configuration.

From the repository root:

```powershell
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
```

The publish output is written to:

```text
artifacts/
└── plugin/
    ├── MediaVault.Jellyfin.dll
    └── ...
```

The exact contents of the output directory may vary depending on the project and .NET publish output.

The primary plugin assembly is:

```text
MediaVault.Jellyfin.dll
```

---

# 📁 8. Build Output

The repository uses separate locations for generated development and release output.

### Development Build Output

Normal .NET builds may create:

```text
bin/
obj/
```

These are generated by the .NET SDK and should not be manually edited.

### Release Output

The documented release publish command places its output in:

```text
artifacts/plugin/
```

This directory is intended to provide a clean location from which release assets can be assembled.

---

# 🗜️ 9. Manual Release Packaging

Release packaging is currently a manual process.

After publishing the plugin, the resulting DLL can be packaged into a ZIP file with PowerShell.

For example:

```powershell
Compress-Archive `
    -Path .\artifacts\plugin\MediaVault.Jellyfin.dll `
    -DestinationPath .\artifacts\MediaVault.Jellyfin.zip `
    -Force
```

This produces:

```text
artifacts/
├── MediaVault.Jellyfin.zip
└── plugin/
    └── MediaVault.Jellyfin.dll
```

The resulting ZIP can then be uploaded as a GitHub Release asset.

---

# 📦 10. Release Assets

A release can contain the plugin binary alongside the separately maintained JavaScript Injector assets.

Typical plugin release assets include:

```text
MediaVault.Jellyfin.dll
MediaVault.Jellyfin.dll.zip
```

The JavaScript component may also be distributed alongside the plugin release where appropriate, for example:

```text
Media_Vault.js
jellyfin-javascript-injector-scripts.json
Plugin.+.JS.Injection.zip
```

These JavaScript files are release/distribution assets rather than source files in the C# plugin project.

The exact release asset set may change between releases.

---

# 🧩 11. Source Code Structure

The main plugin source is located at:

```text
src/MediaVault.Jellyfin/
```

The source is organised by responsibility.

```text
src/MediaVault.Jellyfin/
│
├── Catalogue/
│   ├── CatalogueHostedService.cs
│   ├── CatalogueStore.cs
│   └── Models.cs
│
├── Channels/
│   ├── ChannelItemFactory.cs
│   └── MediaVaultChannel.cs
│
├── Configuration/
│   ├── configPage.html
│   ├── DownloadPage.html
│   └── PluginConfiguration.cs
│
├── Controllers/
│   ├── CatalogueController.cs
│   ├── ConnectionController.cs
│   └── DownloadController.cs
│
├── Services/
│   ├── GPhotosDlService.cs
│   ├── MediaVaultSortTitleHostedService.cs
│   └── ServiceRegistrator.cs
│
├── MediaVault.Jellyfin.csproj
└── Plugin.cs
```

---

# 📚 12. Catalogue Code

The catalogue implementation is located under:

```text
src/MediaVault.Jellyfin/Catalogue/
```

### `CatalogueStore.cs`

`CatalogueStore` is responsible for loading and maintaining the Media Vault catalogue used by the Jellyfin Channels.

Its responsibilities include:

* Reading the configured catalogue source.
* Validating catalogue data.
* Mapping source data into plugin models.
* Maintaining the last-known-good catalogue.
* Performing cache replacement safely.

The catalogue can be sourced from the configured local file or HTTP/HTTPS endpoint.

---

### `CatalogueHostedService.cs`

The hosted service manages background catalogue-related behaviour such as refresh operations.

It allows catalogue state to be maintained without requiring each Channel request to independently retrieve the external catalogue.

---

### `Models.cs`

Contains the catalogue and domain models used by the plugin.

Changes to these models should be considered carefully because they can affect:

* Catalogue deserialisation.
* Channel generation.
* Download identification.
* Tests.
* Existing catalogue compatibility.

---

# 📺 13. Channel Code

The Channel implementation is located under:

```text
src/MediaVault.Jellyfin/Channels/
```

The plugin exposes three main Jellyfin Channels:

```text
Movies
TV Shows
Formula 1
```

---

## `MediaVaultChannel.cs`

This contains the Jellyfin Channel implementation.

The Channel exposes catalogue content using Jellyfin's supported Channel architecture.

The plugin does not pretend that cloud-only catalogue entries are already normal local Jellyfin media files.

---

## `ChannelItemFactory.cs`

This handles the creation of Channel folders and items from Media Vault catalogue data.

It is responsible for translating the catalogue hierarchy into Jellyfin-native Channel structures.

Changes here should normally be accompanied by updates to the relevant unit tests.

---

# 🌐 14. Controller Code

The controller implementations are located under:

```text
src/MediaVault.Jellyfin/Controllers/
```

---

## `CatalogueController.cs`

Provides HTTP endpoints related to catalogue access and the catalogue information exposed to the Media Vault integration.

---

## `ConnectionController.cs`

Provides connection-related endpoints used by the Media Vault integration and configuration/interface components.

---

## `DownloadController.cs`

Coordinates download-related requests between Jellyfin, the plugin, and GPhotosDL.

The download API includes operations for:

```text
Start
Status
Catalogue
Cancel
```

The plugin uses the Media Vault ID to associate Jellyfin Channel items with their corresponding GPhotosDL download.

---

# ⬇️ 15. GPhotosDL Integration

The GPhotosDL integration is implemented by:

```text
src/MediaVault.Jellyfin/Services/GPhotosDlService.cs
```

The plugin communicates with GPhotosDL through HTTP.

The important GPhotosDL operations include:

```text
POST /download
GET  /download/{id}
DELETE /download/{id}
```

The plugin uses GPhotosDL as the source of truth for download progress.

The plugin does not itself implement Google Photos authentication or the underlying Google Photos download mechanism.

---

## Growing Media Files

GPhotosDL downloads the real media file to disk.

During an active download, the file may still be growing.

The integration therefore has to account for the difference between:

```text
Catalogue item exists
```

and:

```text
Real local media file is available
```

Playback resolution and targeted file discovery are documented in detail in:

[`ARCHITECTURE.md`](./ARCHITECTURE.md)

---

# ▶️ 16. Playback Development

The plugin deliberately uses Jellyfin's normal media pipeline rather than manufacturing a fake playback source.

The important development flow is:

```text
Jellyfin Channel Item
        │
        ▼
Download Requested
        │
        ▼
GPhotosDL Downloads Real .mkv
        │
        ▼
Plugin Resolves Exact Local Path
        │
        ▼
Jellyfin Resolves Real BaseItem
        │
        ▼
Jellyfin Produces Real MediaSourceInfo
        │
        ▼
Jellyfin Client Playback
```

This means playback-related changes should be tested against the actual Jellyfin media pipeline rather than only checking whether a Channel item appears.

The plugin should not introduce fake `MediaSourceInfo` objects or fake media files as a shortcut around Jellyfin's normal media resolution.

See [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the full playback architecture.

---

# 🔎 17. Targeted File Discovery

The plugin uses targeted file/item discovery when it needs Jellyfin to recognise newly downloaded local media.

The implementation targets the expected media path/item rather than triggering an unnecessary whole-library scan.

This is important because a full library scan can introduce unnecessary disk I/O, particularly for large media libraries.

When modifying this area, test at least:

1. A catalogue item is selected.
2. GPhotosDL begins downloading.
3. The expected local filename is created.
4. The plugin waits for the required file readiness conditions.
5. Jellyfin discovers the expected item.
6. Jellyfin resolves the real `BaseItem`.
7. Jellyfin exposes the real `MediaSourceInfo`.
8. The client can begin playback.

---

# 🔤 18. Sort Title Synchronisation

The plugin contains:

```text
Services/MediaVaultSortTitleHostedService.cs
```

This service maintains Jellyfin sort-title behaviour for Channel-backed Movie items.

The implementation uses Jellyfin item events and targeted queries rather than relying on a conventional full-library scan.

Changes to sort-title behaviour should therefore be tested against actual Jellyfin Channel items.

---

# ⚙️ 19. Configuration Code

Configuration-related source files are located under:

```text
src/MediaVault.Jellyfin/Configuration/
```

The main configuration model is:

```text
PluginConfiguration.cs
```

The HTML files provide Jellyfin configuration/interface pages.

Configuration behaviour itself is documented in:

[`CONFIGURATION.md`](./CONFIGURATION.md)

Development changes to configuration should consider:

* Existing configuration values.
* Default values.
* Backwards compatibility.
* Jellyfin plugin configuration serialisation.
* Runtime services consuming the configuration.

---

# 🧱 20. Service Registration

Service registration is handled through:

```text
src/MediaVault.Jellyfin/Services/ServiceRegistrator.cs
```

This is where the plugin's services are registered with the Jellyfin dependency injection system.

When adding a new long-lived service or hosted service, review the existing registration pattern before introducing a new lifetime or dependency.

---

# 🔌 21. Plugin Entry Point

The main plugin entry point is:

```text
src/MediaVault.Jellyfin/Plugin.cs
```

This defines the Jellyfin plugin itself and connects the plugin into Jellyfin's plugin system.

Changes to plugin identity, configuration registration, or lifecycle behaviour should be made carefully because they affect installation and existing plugin instances.

The plugin GUID is:

```text
f0413623-c78e-45e7-a114-352b0bfb205f
```

---

# 🌐 22. JavaScript Injector Component

The Media Vault JavaScript interface is a separate component from the C# plugin source.

It is used to provide the Media Vault-specific Jellyfin web interface, including functionality such as:

* Download controls.
* Download progress.
* Download status.
* Download cancellation.
* Cached runtime information.
* Download speed.
* Estimated completion information.

The JavaScript source is maintained separately and should **not** be treated as a `JS Injection/` source directory inside this repository.

Release assets may still be distributed alongside plugin releases for convenience.

The JavaScript component should therefore be developed, tested, and released independently from the C# project where appropriate.

---

# 🧪 23. Testing Against a Live Jellyfin Server

Unit tests cannot reproduce every behaviour of a running Jellyfin server.

After making changes that affect Jellyfin integration, perform manual testing against a Jellyfin 10.11.11 development/test environment.

Useful areas to test include:

### Catalogue

* Catalogue loading.
* Catalogue refresh.
* Last-known-good fallback.
* Invalid catalogue handling.

### Channels

* Movies appear correctly.
* TV hierarchy is correct.
* Formula 1 hierarchy is correct.
* Metadata is displayed correctly.

### Downloads

* Download starts.
* Download progress updates.
* Download cancellation works.
* Existing local files are recognised correctly.

### Playback

* The expected local file is created.
* Jellyfin discovers the real item.
* A real Jellyfin media source is returned.
* Playback starts through the normal Jellyfin pipeline.

### Sort Titles

* Channel-backed Movie items receive the expected sort titles.
* Updates occur without requiring a full-library scan.

---

# 🐛 24. Debugging and Diagnostics

When investigating a development issue, start by determining which layer is actually failing.

```text
Catalogue
    │
    ▼
Channel
    │
    ▼
Download Controller
    │
    ▼
GPhotosDL
    │
    ▼
Local File
    │
    ▼
Jellyfin Discovery
    │
    ▼
BaseItem
    │
    ▼
MediaSourceInfo
    │
    ▼
Client Playback
```

This separation is particularly useful because a failure in one layer does not necessarily indicate a problem in the layers before or after it.

For example:

```text
Catalogue item visible
        +
Download succeeds
        +
File exists
        +
Jellyfin cannot resolve BaseItem
```

is a different problem from:

```text
Catalogue item visible
        +
Download never starts
```

Likewise:

```text
BaseItem resolved
        +
MediaSourceInfo resolved
        +
Client cannot play
```

should be investigated as a playback/client compatibility issue rather than as a catalogue problem.

See [`TROUBLESHOOTING.md`](./TROUBLESHOOTING.md) for operational troubleshooting procedures.

---

# 📖 25. Development Documentation

The repository documentation is divided by purpose.

| Document                                               | Purpose                                                                     |
| ------------------------------------------------------ | --------------------------------------------------------------------------- |
| [`README.md`](../README.md)                            | Main project overview and landing documentation                             |
| [`INSTALLATION.md`](./INSTALLATION.md)                 | Installing and setting up the plugin                                        |
| [`CONFIGURATION.md`](./CONFIGURATION.md)               | Plugin configuration and runtime settings                                   |
| [`ARCHITECTURE.md`](./ARCHITECTURE.md)                 | Runtime architecture and system design                                      |
| [`DEVELOPMENT.md`](./DEVELOPMENT.md)                   | Building, testing, modifying, and releasing the project                     |
| [`TROUBLESHOOTING.md`](./TROUBLESHOOTING.md)           | Diagnosing installation and runtime problems                                |
| [`CHANNEL_ARCHITECTURE.md`](./CHANNEL_ARCHITECTURE.md) | Technical notes on Jellyfin Channel implementation                          |
| [`NATIVE_LIBRARY_SPIKE.md`](./NATIVE_LIBRARY_SPIKE.md) | Technical investigation notes relating to native Jellyfin library behaviour |

Development documentation should explain **how the project is built and maintained**, while detailed runtime architecture belongs in `ARCHITECTURE.md`.

---

# 📝 26. Making Code Changes

When making a change, identify the smallest component responsible for the behaviour.

Examples:

| Change                        | Primary Location                                            |
| ----------------------------- | ----------------------------------------------------------- |
| Catalogue parsing/mapping     | `Catalogue/`                                                |
| Catalogue refresh/caching     | `Catalogue/CatalogueStore.cs` / `CatalogueHostedService.cs` |
| Movie/TV/F1 Channel structure | `Channels/`                                                 |
| Download API behaviour        | `Controllers/DownloadController.cs`                         |
| GPhotosDL communication       | `Services/GPhotosDlService.cs`                              |
| Sort-title behaviour          | `Services/MediaVaultSortTitleHostedService.cs`              |
| Plugin configuration          | `Configuration/`                                            |
| Dependency registration       | `Services/ServiceRegistrator.cs`                            |
| Plugin identity/lifecycle     | `Plugin.cs`                                                 |
| Unit tests                    | `tests/MediaVault.Jellyfin.Tests/`                          |

Avoid moving functionality between layers simply to make a local change easier.

The existing separation between catalogue handling, Channel generation, controllers, external download communication, and Jellyfin integration is intentional.

---

# 🔁 27. Recommended Development Cycle

For a normal code change:

### 1. Modify the relevant source

Make the change in the appropriate source directory.

### 2. Restore dependencies if required

```powershell
dotnet restore
```

### 3. Run the automated tests

```powershell
dotnet test .\MediaVault.Jellyfin.sln
```

### 4. Publish the plugin

```powershell
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
```

### 5. Test the published plugin

Install or replace the development build in the Jellyfin test environment.

### 6. Perform relevant manual tests

Test the specific feature as well as any affected integration points.

### 7. Package the release

Create the required release assets from the published output.

### 8. Commit source changes

Only source, configuration, project metadata, tests, and documentation belonging in the repository should be committed.

Generated build output should remain excluded.

---

# 🏷️ 28. Versioning and Releases

The plugin version is represented in the repository's Jellyfin build/plugin metadata.

Before creating a release, verify that:

* The source builds successfully.
* Automated tests pass.
* The published DLL is produced successfully.
* The plugin has been manually tested where appropriate.
* Release metadata matches the intended release.
* Release assets correspond to the published source.

A release should represent a known-good combination of:

```text
Source
  +
Plugin Metadata
  +
Published DLL
  +
Required Distribution Assets
```

---

# 🚫 29. What Not to Commit

Do not commit generated .NET build output such as:

```text
bin/
obj/
```

Do not commit temporary diagnostic output such as:

```text
restore-diag.txt
```

Do not treat published DLLs as normal source files.

Release binaries should be distributed through the release process rather than being added to the source tree.

---

# 🔬 30. Technical Investigation Notes

Two documents contain lower-level investigation material:

### `CHANNEL_ARCHITECTURE.md`

Contains technical notes concerning the Jellyfin Channel implementation and related architectural decisions.

### `NATIVE_LIBRARY_SPIKE.md`

Contains investigation/prototype notes relating to native Jellyfin library behaviour.

These documents are useful when investigating specific Jellyfin behaviours, but they are not intended to replace the main architecture or development documentation.

---

# ⚠️ 31. Development Considerations

The plugin interacts with several systems:

```text
Media Vault
     │
     ▼
Catalogue
     │
     ▼
Media Vault Jellyfin Plugin
     │
     ├──────────────► Jellyfin Channels
     │
     ├──────────────► Download API
     │
     ▼
GPhotosDL
     │
     ▼
Google Photos
     │
     ▼
Real Local Media File
     │
     ▼
Jellyfin Media Pipeline
     │
     ▼
Client
```

A change that appears local can therefore have integration consequences.

In particular, changes involving:

* Media Vault IDs.
* Catalogue models.
* Local filenames.
* Download paths.
* GPhotosDL API behaviour.
* Jellyfin Channel items.
* Targeted file discovery.
* BaseItem resolution.
* MediaSourceInfo generation.

should be tested beyond the immediate method or class being changed.

---

# 📌 32. Quick Reference

## Restore

```powershell
dotnet restore
```

## Test

```powershell
dotnet test .\MediaVault.Jellyfin.sln
```

## Publish

```powershell
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
```

## Package DLL

```powershell
Compress-Archive `
    -Path .\artifacts\plugin\MediaVault.Jellyfin.dll `
    -DestinationPath .\artifacts\MediaVault.Jellyfin.zip `
    -Force
```

## Main project

```text
src/MediaVault.Jellyfin/MediaVault.Jellyfin.csproj
```

## Test project

```text
tests/MediaVault.Jellyfin.Tests/MediaVault.Jellyfin.Tests.csproj
```

## Solution

```text
MediaVault.Jellyfin.sln
```

## Jellyfin target

```text
10.11.11
```

## .NET target

```text
net9.0
```

## Plugin GUID

```text
f0413623-c78e-45e7-a114-352b0bfb205f
```

---

# 📚 Related Documentation

* [Installation](INSTALLATION.md)
* [Configuration](CONFIGURATION.md)
* [Architecture](ARCHITECTURE.md)
* [Troubleshooting](TROUBLESHOOTING.md)
* [Channel Architecture](CHANNEL_ARCHITECTURE.md)

[Return to README](../README.md)
