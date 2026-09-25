# 🛠️ Development & Build Guide

This document provides a technical overview for building, testing, packaging, and understanding the code structure of the **Media Vault Jellyfin Plugin**.

---

## 🏗️ 1. Building From Source

### 📋 Requirements

To build, test, and modify the plugin locally, ensure you have the following installed:

* **.NET 9 SDK**
* **Git**
* A **Jellyfin 10.11.11** development/test environment (if testing against a live server)

The core build configuration files in the repository root are:

* `MediaVault.Jellyfin.sln` — C# solution file containing the core plugin and unit test projects.
* `Directory.Packages.props` — Central Package Management (CPM) configuration file for NuGet dependencies.
* `build.yaml` — Jellyfin plugin/build metadata, including the plugin name, GUID, version, target ABI, framework, overview, description, category, owner, and artifacts.

---

### 📦 Restore Dependencies

To restore all required NuGet packages, execute from the repository root:

```bash
dotnet restore
```

---

### 🔨 Build the Plugin

The plugin itself is built directly from its project file using the `Release` configuration:

```powershell
dotnet build -c Release .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj
```

This is the build command used during development. The generated plugin DLL is located at:

```text
jellyfin-media-vault\src\MediaVault.Jellyfin\bin\Release\net9.0\MediaVault.Jellyfin.dll
```

For a local Jellyfin installation, copy that DLL into:

```text
Jellyfin\Data\plugins\MediaVault.Jellyfin
```

This is the same DLL installation process described in **steps 4 and 5 of `INSTALLATION.md`**.

After replacing the DLL, restart Jellyfin so that the updated plugin is loaded.

---

### 🧪 Run Unit Tests

Execute the automated test suite to verify catalogue mapping, models, and channel creation logic:

```bash
dotnet test .\MediaVault.Jellyfin.sln
```

### 🧾 Test Coverage Scope

The unit test suite covers core domain logic, including:

* Catalogue JSON deserialisation and DTO transformation.
* Jellyfin Channel folder and item structure generation.

---

### 🚀 Publish the Plugin

For a packaged release output, publish the plugin project using the `Release` configuration:

```powershell
dotnet publish .\src\MediaVault.Jellyfin\MediaVault.Jellyfin.csproj -c Release -o .\artifacts\plugin
```

The published plugin DLL will be placed inside:

```text
artifacts\plugin\MediaVault.Jellyfin.dll
```

The published output is useful when preparing release assets. For normal local development and installation, the direct `dotnet build` output described above is the build output used for installation.

---

### 🗜️ Create a Manual Installation ZIP

To package the published DLL into a compressed release bundle using PowerShell:

```powershell
Compress-Archive `
    -Path .\artifacts\plugin\MediaVault.Jellyfin.dll `
    -DestinationPath .\artifacts\MediaVault.Jellyfin.zip `
    -Force
```

This ZIP archive can then be uploaded directly to GitHub Releases.

---

## 📦 2. Release Packaging & Structure

Generated build outputs should **never** be committed directly to source control. Release binaries belong in GitHub Releases.

### Source Tree vs. Release Artifacts

The repository source tree stores source code and build definitions. Artifacts produced during the build or publish process, such as:

* `MediaVault.Jellyfin.dll`
* `MediaVault.Jellyfin.pdb`
* `MediaVault.Jellyfin.deps.json`
* `MediaVault.Jellyfin.xml`

are generated dynamically during local builds, publishing, or release packaging.

---

### 🏷️ Official Release Assets

Each release tag provides individual components alongside a bundled package:

1. **Standalone Plugin DLL:**
   `MediaVault.Jellyfin.dll` *(Required for standard manual Jellyfin plugin installation)*
2. **Standalone Web UI Assets:**
   * `Media_Vault.js`
   * `jellyfin-javascript-injector-scripts.json` *(Required for JS Injector setup)*
3. **All-in-One Package:**
   `Plugin.+.JS.Injection.zip` *(Contains the plugin DLL and both JS Injection files)*

---

## 🧩 3. Architecture & Code Structure

The backend plugin architecture strictly separates Jellyfin core integration from external Google Photos downloading responsibilities.

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

### 🧱 Component Responsibility Breakdown

* **CatalogueStore:** Ingests, validates, maps, and caches the external Media Vault JSON catalogue using an atomic write strategy.
* **Channels:** Maps catalogue entries into Jellyfin-native Channel folders and items without executing raw database queries against `jellyfin.db`.
* **DownloadController:** Exposes Jellyfin REST API endpoints for starting, monitoring, and cancelling download tasks.
* **GPhotosDlService:** Communicates directly with the `GPhotosDL` daemon via standard HTTP endpoints.

---

## 🌐 4. Web UI Development & Injection

The JavaScript injection component operates independently of the C# plugin codebase.

### 📁 Workspace Directory Layout

```text
JS Injection/
├── Media-Vault-JS-Injection.js
└── jellyfin-javascript-injector-scripts.json
```

### 🔁 Format Equivalence

Both `Media-Vault-JS-Injection.js` (raw JavaScript) and `jellyfin-javascript-injector-scripts.json` (exported configuration) represent the same Media Vault web interface:

* **JSON Export:** Allows single-click imports into the JS Injector plugin.
* **Raw `.js` File:** Provides a readable format for local development, debugging, or manual copy-paste setups.

# 📚 Related Documentation

* [Installation](INSTALLATION.md)
* [Configuration](CONFIGURATION.md)
* [Architecture](ARCHITECTURE.md)
* [Troubleshooting](TROUBLESHOOTING.md)
* [Channel Architecture](CHANNEL_ARCHITECTURE.md)

[Return to README](../README.md)
