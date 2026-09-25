# Installation Guide

This guide will walk you through setting up the **Media Vault Jellyfin Plugin** step by step in plain, simple terms.

---

## 📋 Pre-requisites & Summary

### What You Will Do in This Guide
* Install two required third-party plugins: **File Transformation** and **JS Injector**.
* Install the **Media Vault Plugin** (`MediaVault.Jellyfin.dll`).
* Add the custom Media Vault script into the **JS Injector** plugin.

> [!NOTE]
> **Operating System Note:** This guide assumes you have already completed the initial setup/installation of Jellyfin and are using a **Windows** device. If you are using Linux, macOS, or Docker, you can still follow these steps—you will simply need to locate your Jellyfin `plugins` folder on that specific operating system. If you are currently installing Jellyfin for the first time, take note of where the Server files are saved, as knowing this path will make Step 4 much easier.

---

### Required Downloads
Head to the **Releases** section of this GitHub repository and locate the **Assets** section to download the required files:

1. **`MediaVault.Jellyfin.dll`** (approx. 240 KB) – Needed for Step 3 to install this plugin. *(Alternatively, you can download `Plugin.+.JS.Injection.zip` which contains all files bundled together).*
2. **`jellyfin-javascript-injector-scripts.json`** (approx. 83.2 KB) – Recommended for Step 7 to import the script into JS Injector.
3. **`Media_Vault.js`** (approx. 113 KB) – Backup file used in Step 7.5 if importing the JSON file directly fails or becomes obsolete in future versions.

---

## 🗺️ Layout of the Guide

To keep things as smooth as possible, the primary path below covers the easiest, recommended installation methods. Alternative or manual methods (such as manually unhiding folders or copy-pasting JS code) are included in expandable drop-down boxes directly under their respective steps.

* **Step 1:** Add the two third-party plugin repositories.
* **Step 2:** Install File Transformation & JS Injector.
* **Step 3:** Prepare the Media Vault plugin DLL.
* **Step 4:** Locate the Jellyfin `plugins` folder using a Run command *(or manual method)*.
* **Step 5:** Create the plugin folder and move the DLL file.
* **Step 6:** Configure the Media Vault Cloud Catalogue settings.
* **Step 7:** Load the JavaScript file into JS Injector *(or manual copy/paste method)*.
* **Step 8 (Optional):** Fix visual caching issues in your web browser.

---

## 🛠️ Step-by-Step Installation

### Step 1: Add the Plugin Repositories
1. Open Jellyfin and navigate to the **Dashboard**.
2. Go to **Plugins** > **Manage Repositories** > click **+ New Repository**.
3. You need to add two external repositories:
   * **File Transformation:**
     * **Repository Name:** `File Transformation`
     * **Repository URL:** `https://www.iamparadox.dev/jellyfin/plugins/manifest.json`
     * Click **Add**.
   * **JS Injector:**
     * **Repository Name:** `JS Injector`
     * **Repository URL:** `https://raw.githubusercontent.com/n00bcodr/jellyfin-plugins/main/10.11/manifest.json`
     * Click **Add**.

---

### Step 2: Install File Transformation & JS Injector
1. Go back to the **Plugins** page in the Dashboard and click the **Available** tab at the top.
2. Find **File Transformation** and **JS Injector**.
3. Click on each plugin individually and press the **Install** button.
4. Once both are installed, restart Jellyfin:
   * Click **Restart** in the Jellyfin Dashboard.
   * *Alternatively (on Windows):* Find the Jellyfin icon in your system tray (bottom-right of your screen), right-click it, click **Stop Server**, and then click **Start Server**.
5. After Jellyfin restarts, go back to **Dashboard** > **Plugins** and verify that both plugins are listed.

---

### Step 3: Prepare the Media Vault Plugin File
1. Download `MediaVault.Jellyfin.dll` directly from the **Releases** section of this repository.
2. *Alternative:* If you downloaded `Plugin.+.JS.Injection.zip`, save it somewhere easy to find and extract the ZIP file to access the `.dll` file inside.

---

### Step 4: Locate the Jellyfin Plugins Folder
You need to navigate to Jellyfin's `plugins` folder on your computer. If you installed Jellyfin in the default location on Windows, follow **Method 1**.

#### Method 1: Using the Run Command (Recommended)
1. Press the **Windows Key + R** on your keyboard at the same time to open the **Run** window.
2. Type `%programdata%` into the text box and press **Enter** (or click **OK**).
3. File Explorer will open in the hidden `ProgramData` directory.
4. Double-click the **Jellyfin** folder, then **Server**, then **plugins**.
5. If you are in the right place, you will see two folders corresponding to the plugins installed in Step 2.

<details>
<summary><b>Click here for Step 4.5: Alternative Method (Locating the folder manually in File Explorer)</b></summary>

<br>

If you do not wish to use the Run command, you must enable hidden files in Windows File Explorer first:

1. Open File Explorer by pressing **Windows Key + E**.
2. Click the **three horizontal dots (...)** on the top menu bar and select **Options** at the bottom.
3. In the **Folder Options** window, click the **View** tab at the top.
4. Under **Advanced settings**, scroll down to **Hidden files and folders**.
5. Select the radio button for **"Show hidden files, folders, and drives"**.
6. Click **Apply**, then click **OK**.
7. Now navigate manually using File Explorer to:
   `C:\ProgramData\Jellyfin\Server\plugins`

*(If you chose a custom installation path when setting up Jellyfin, go to that custom location's `Server\plugins` folder).*

---

</details>

---

### Step 5: Install Media Vault DLL & Restart
1. Inside the `plugins` folder, create a **new folder** and name it exactly:
   `MediaVault.Jellyfin`
2. Move the `MediaVault.Jellyfin.dll` file you downloaded in Step 3 into this new `MediaVault.Jellyfin` folder.
3. Restart the Jellyfin server (via the system tray icon or the **Restart** button in the Jellyfin Dashboard).
4. Once restarted, go to **Dashboard** > **Plugins** > **Installed**. You should now see all three plugins listed.

---

### Step 6: Configure the Media Vault Plugin Settings
1. Go to the Jellyfin Dashboard and open the settings page for **Media Vault Cloud Catalogue**.
2. You will see a header that reads:
   > *"Media Vault Cloud Catalogue<br>Cloud-backed Media Vault films are browsed here without being represented as fake local media files."*
3. Fill out the configuration fields:
   * **Catalogue JSON URL or server-local path:** Enter the link/path to the JSON file that populates your Cloud Collections (Movies, TV Shows, and F1 content).
   * **GPhotosDL server URL:** Enter the HTTP API link created when you run `.\gphotosdl.exe -show -debug` (required for Google Photos downloads).
   * **Download Directories:** Set these paths to match the exact local storage locations assigned to your Jellyfin libraries.
     * *Example:* If your local Movie library points to `C:\Users\username\Videos\Movies`, use that exact path in the Movies Download Directory field. If you only have a Movie library, create folders on your drive for TV Shows and F1 and link those accordingly.
   * **Buffer & Performance Settings:** You can leave the default numbers initially. If media buffers or fails to load later, you can fine-tune these values.
4. Click **Save settings** at the bottom of the page.

---

### Step 7: Load the Script into JS Injector
1. Go to the settings page for the **JS Injector** plugin.
2. You should see buttons labeled **Add Script**, **Export All**, and **Import**.

#### Recommended Method: Importing the JSON File
1. Click the **Import** button.
2. Select the `jellyfin-javascript-injector-scripts.json` file you downloaded earlier.
3. The script will load with the name **Media Vault**.
4. Ensure the checkbox to enable the script is **checked**, make sure **Enabled** is active, and click **Save**.
5. **Verify Installation:**
   * Go to the Jellyfin Homepage. Under **My Media**, you should see three Cloud Directory items. *(Press **F5** to refresh the page if they do not appear immediately).*
   * Click on a movie, show, or F1 event. A status window will appear giving you real-time download status information *(Note: `GPhotosDL` must be running on your system for live status updates).*

<details>
<summary><b>Click here for Step 7.5: Alternative Method (Copy & Pasting Media_Vault.js manually)</b></summary>

<br>

If importing the `.json` file fails or if the import tool changes in newer JS Injector updates, follow these manual steps:

1. Locate `Media_Vault.js` on your computer.
2. Right-click the file and select **Open with** > **Notepad** *(this prevents Windows from trying to execute the JavaScript file)*.
3. Select all the text by pressing **CTRL + A**, then copy it by pressing **CTRL + C**.
4. Return to the **JS Injector** settings page in Jellyfin.
5. Click **Add Script**.
6. Set the **Script Name** to `Media Vault`.
7. Click inside the large code box below the name and press **CTRL + V** to paste the code *(around 2,183 lines of code will be pasted)*.
8. Ensure the script is enabled and click **Save**.

---

</details>

---

### Step 8 (Optional): Fix Web UI Visual Characters Bug
If you notice strange text or corrupted characters appearing in your web browser UI:

1. Open Developer Tools in your browser by pressing **F12** (or **CTRL + SHIFT + I**, or right-clicking anywhere and choosing **Inspect**).
2. Click the **Network** tab at the top.
3. Check the box labeled **Disable cache**.
4. Close Developer Tools (click the **X** or press **F12** again).
5. Refresh the page (**F5**). The visual artifacts should now be resolved.

---

🎉 **Everything is now installed!** You can rename, reorder, and customize your Cloud Collections however you prefer inside Jellyfin.

# 📚 Related Documentation

* [Configuration](CONFIGURATION.md)
* [Development](DEVELOPMENT.md)
* [Architecture](ARCHITECTURE.md)
* [Troubleshooting](TROUBLESHOOTING.md)
* [Channel Architecture](CHANNEL_ARCHITECTURE.md)

[Return to README](../README.md)
