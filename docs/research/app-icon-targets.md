# Windows App Icon Targets & Discovery Research

- **Date:** 2026-11-07
- **Target OS:** Windows 10 & Windows 11 (x64 / ARM64)
- **Scope:** Cleanroom analysis of installed applications discovery, icon extraction, target modification safety, and transactional restoration.

---

## 1. Executive Summary

Customizing Windows application icons differs fundamentally from customizing folder icons (`desktop.ini`):
1. **Executable files (`.exe`)** contain embedded icon resources inside PE (Portable Executable) binary structures (`.rsrc`). Modifying PE headers or resource tables directly on disk invalidates digital signatures (Authenticode), trips Antivirus/EDR heuristics, causes application crashes, and fails outright on protected or running binaries.
2. **Packaged applications (MSIX / UWP / Modern Store Apps)** are deployed inside cryptographically sealed package directories (`%ProgramFiles%\WindowsApps`) with AppX manifests. Their tiles and taskbar icons are managed dynamically by the Shell Application Model (`AppUserModelID`) and cannot be redirected via simple shortcut attributes.
3. **Application Shortcuts (`.lnk`)** in the Windows Start Menu and Desktop are the primary, sanctioned, and native mechanism through which Windows users launch and view desktop applications. The Win32 Shell API explicitly supports custom icon redirection via `IShellLink::SetIconLocation` without modifying executable binaries or requiring administrator privileges when targeting per-user directories.

Therefore, the **first supported target** for Windows Icons Admin is **User Shell Shortcuts (`.lnk`)** located in the current user's profile.

---

## 2. Research Questions & Findings

### Q1: What sources allow enumerating installed applications in Windows?

There are 4 distinct discovery sources on Windows:

| Source | File System / Registry Location | Types of Apps Discovered | UAC Required |
| :--- | :--- | :--- | :--- |
| **User Start Menu Shortcuts** | `%APPDATA%\Microsoft\Windows\Start Menu\Programs` | Win32 apps installed per-user or registered for current user | **No** |
| **Common Start Menu Shortcuts** | `%ProgramData%\Microsoft\Windows\Start Menu\Programs` | Win32 apps installed machine-wide | Read: No / Write: **Yes** |
| **Desktop Shortcuts** | `%USERPROFILE%\Desktop` and `%PUBLIC%\Desktop` | Desktop application shortcuts placed during install | User: No / Public: **Yes** |
| **Windows Uninstall Registry** | `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall`<br>`HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall`<br>`HKLM\Software\WOW6432Node\...` | Win32 install registrations (DisplayIcon, InstallLocation) | No (Read-only) |
| **Packaged App Repository** | Windows AppX / MSIX Package Manager (`Get-AppxPackage`) | UWP and Store packaged applications | No (Read-only) |

#### Discovery Hierarchy for Windows Icons Admin:
1. Scan `%APPDATA%\Microsoft\Windows\Start Menu\Programs` recursively for `.lnk` files.
2. Scan `%USERPROFILE%\Desktop` for `.lnk` files.
3. Scan `%ProgramData%\Microsoft\Windows\Start Menu\Programs` recursively (cataloged as *Partially Supported* / requiring Admin).
4. Correlate with Uninstall Registry entries to resolve rich display metadata (publisher, version, official `DisplayIcon`).

---

### Q2: What stable identity can be extracted from each source?

An installed application needs a deterministic, collision-resistant identifier that remains stable across reboots and styling operations:

1. **Target Executable Identity**:
   - Resolved target path of the `.lnk` (e.g. `%LOCALAPPDATA%\Programs\Spotify\Spotify.exe`).
   - Normalization: resolved to canonical lowercase path with environment variable expansion.
2. **App Identifier (`AppId`)**:
   - `SHA-256` hash of the normalized target executable path (or normalized `.lnk` relative path if the target is non-file or argument-driven).
   - Format: `app-{sha256:16}` (e.g., `app-7f8a9b2c3d4e5f60`).
3. **Display Name**:
   - Shortcut base name without `.lnk` extension (e.g. `Visual Studio Code`).
   - Cleaned of generic suffixes (`- Shortcut`, `(x64)`).

---

### Q3: How is the original icon extracted?

Applications specify their icons through several mechanisms:
1. **Shortcut Custom Icon**: The shortcut's `GetIconLocation` returns a non-empty icon path and index.
2. **Target Executable Resource**: If `GetIconLocation` is empty or points to the target executable itself, the icon is embedded inside the target `.exe` or `.dll` as a `RT_GROUP_ICON` resource.
3. **Uninstall Registry `DisplayIcon`**: Explicit `.ico` or `.exe` path stored in the Windows Uninstall key.

#### Extraction Pipeline:
1. Query shortcut via `IShellLinkW::GetIconLocation`.
2. If location is set, resolve resource:
   - If it points to an `.ico` or `.png`, read directly.
   - If it points to an `.exe` or `.dll`, extract the group icon via `ExtractIconExW` / `SHDefExtractIconW` or `PrivateExtractIconsW` at `256x256` or highest available resolution.
3. If no shortcut icon is assigned, inspect the resolved target executable using `PrivateExtractIconsW`.
4. Render extracted icon to a 32-bit ARGB bitmap (in memory), preserving the complete alpha channel.
5. Save snapshot to `%LOCALAPPDATA%\WindowsIconsAdmin\OriginalSnapshots\{AppId}.png` as the immutable baseline for restore.

---

### Q4: Where does Windows display application icons?

| Shell Surface | Mechanism Used by Windows | Impact of `.lnk` Icon Redirection |
| :--- | :--- | :--- |
| **Start Menu (Pinned & All Apps)** | Reads shortcut `.lnk` metadata and icon cache | **Immediate update** upon `.lnk` rewrite and `SHCNE_UPDATEITEM` / `SHCNE_ASSOCCHANGED` |
| **Desktop** | Reads desktop `.lnk` file | **Immediate update** |
| **Taskbar (Pinned Apps)** | Pinned taskbar shortcuts reside in `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar` | Modifiable via shortcut update |
| **Taskbar (Running Windows)** | Read from window class (`WM_GETICON`, `GCLP_HICON`) sent by process | Unmodified at runtime (requires process restart or window hook; out of scope for MVP) |
| **Alt+Tab / Task View** | Read from process top-level window | Unmodified at runtime |
| **Explorer File Details** | Read from `.exe` resource or shortcut `.lnk` | `.lnk` reflects customized icon |

---

### Q5: Which of these places can be modified safely?

| Target Type | Modifiable? | Safety Evaluation |
| :--- | :--- | :--- |
| **Per-User Shortcuts (`%APPDATA%`, `%USERPROFILE%\Desktop`)** | **YES (Fully Supported)** | Non-destructive. Modifies only the shortcut's `IconLocation` and `IconIndex`. Zero executable tampering. Zero privilege elevation. |
| **Machine-Wide Shortcuts (`%ProgramData%`, `%PUBLIC%\Desktop`)** | **YES (Partial Support)** | Requires Administrator privileges. Can be modified if running elevated, or marked with warning in UI. |
| **Taskbar Pinned Shortcuts** | **YES (Secondary)** | Stored as `.lnk` in User Pinned Taskbar directory. Can be refreshed with shell notification. |
| **Executable Files (`.exe` PE Resources)** | **NO (Strictly Prohibited)** | Destructive. Breaks code signing, Antivirus alerts, violates cleanroom invariants. |
| **MSIX / UWP AppX Packages** | **NO (Unsupported in MVP)** | Package manifests are tamper-protected; requires app manifest manipulation and developer mode. |

---

### Q6: How is the previous state restored?

Restoring an application shortcut is 100% deterministic and atomic:
1. **Pre-modification Snapshot**:
   - Capture `PreviousIconLocation` (string, e.g. `C:\Path\App.exe` or `""`).
   - Capture `PreviousIconIndex` (integer, e.g. `0` or `-1`).
   - Store record in `State/app-undo.json` via `AppIconUndoStore`.
2. **Reversion Process**:
   - If `PreviousIconLocation` was non-empty: call `IShellLinkW::SetIconLocation(PreviousIconLocation, PreviousIconIndex)`.
   - If `PreviousIconLocation` was empty (defaulting to target `.exe`): call `IShellLinkW::SetIconLocation(string.Empty, 0)`.
   - Save the shortcut via `IPersistFile::Save`.
   - Issue `SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, shortcutPath)`.
   - Issue `SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSHNOWAIT, IntPtr.Zero)`.
3. **Artifact Cleanup**:
   - If the applied `.ico` in `GeneratedIcons/` is no longer referenced by any active target, it can be scheduled for cache pruning.

---

### Q7: Which cases require elevated privileges?

- Writing to `%APPDATA%\Microsoft\Windows\Start Menu\Programs`: **Standard user (No UAC)**.
- Writing to `%USERPROFILE%\Desktop`: **Standard user (No UAC)**.
- Writing to `%ProgramData%\Microsoft\Windows\Start Menu\Programs`: **Administrator (UAC required)**.
- Writing to `%PUBLIC%\Desktop`: **Administrator (UAC required)**.

**MVP Policy:** Focus exclusively on standard user paths first (`%APPDATA%` and `%USERPROFILE%\Desktop`). All machine-wide targets are flagged as `PartiallySupported` and skipped unless explicitly confirmed.

---

### Q8: Explicitly unsupported cases for MVP

1. **Direct PE Binary Modification**: Modifying `.rsrc` section of `.exe`/`.dll` is permanently banned.
2. **MSIX / Store Package Re-signing**: Modifying `AppxManifest.xml` or package resources.
3. **Runtime Window Hooking**: Injecting DLLs or sending `WM_SETICON` to running third-party processes.
4. **Third-Party Shell Replacements**: Start11, RetroBar, or custom docks that maintain proprietary icon caches outside standard Windows Shell APIs.

---

## 3. Conclusions for Architecture & Implementation

1. **Domain Isolation**:
   - `WindowsIconsAdmin.Core.Apps` handles representations (`InstalledApp`, `IconTarget`, `AppSupportStatus`).
   - `WindowsIconsAdmin.Core.Styling` handles pure rendering (`IconStyle`, `IconNormalizer`, `IconStyleRenderer`).
   - `WindowsIconsAdmin.Core.Shell.Apps` handles `IShellLink` COM interop and shortcut updates.
2. **Persistence**:
   - `State/app-undo.json` stores `AppBatchRecord` entries.
   - `GeneratedIcons/{cacheKey}.ico` stores rendered multi-resolution icons.
3. **Shell Notification**:
   - Notify each modified `.lnk` with `SHCNE_UPDATEITEM`.
   - Flush shell icon cache with `SHCNE_ASSOCCHANGED` upon batch completion.
