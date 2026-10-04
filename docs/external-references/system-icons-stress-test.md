> **Created:** 2026-10-04
> **Last Updated:** 2026-10-04
> **Status:** Active
> **Scope:** Windows Shell Registry, System Icons, Global Default Folders, File Type Associations

# Adversarial Stress-Test: System Icons, Global Defaults & Shell Associations

## 1. Executive Summary & Verdict
The proposal to expand **WindowsIconsAdmin** into system icons, user profile special folders, global default folder/file icons, and file extensions introduces significant attack surface and operational instability risks in Windows 10 and Windows 11. 

While per-folder `desktop.ini` customization is isolated and low-risk, **modifying global shell registry keys affects the entire operating system**. A single unhandled failure (such as a deleted icon file or corrupt registry entry) can render every folder in Windows Explorer as a black box or invisible glyph.

---

## 2. Attack Vectors & Critical Vulnerabilities

### 2.1 [Critical / Blocker] Windows 11 "Black Box" Icon Corruption via `Shell Icons`
- **Mechanism:** In Windows 11 (22H2 through 24H2), setting `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons` values `"3"` (closed folder) or `"4"` (open folder) to external `.ico` paths frequently triggers icon cache desynchronization. File Explorer's modern XAML views render folders as solid black squares or invisible blank rectangles.
- **Tripwire:** If the custom `.ico` file is moved, renamed, corrupted, or deleted (e.g. during disk cleanup or uninstall), Windows has no fallback and continues attempting to load the missing file, corrupting `IconCache.db`.
- **Hardening Countermeasure:**
  1. **Prefer `HKCU\Software\Classes\Folder\DefaultIcon`** over `HKLM\...\Shell Icons`. `Classes\Folder\DefaultIcon` operates per-user without touching the legacy Shell Icons subsystem, significantly reducing black box corruption risk.
  2. **Immutability & Pinning in Central Cache:** Any icon used for a global default MUST be copied into an immutable, protected directory in `%LOCALAPPDATA%\WindowsIconsAdmin\system-icons\`, keyed by SHA-256 hash. Never reference temporary or user-download folders directly.
  3. **Provide an Instant "Emergency Restore" (Nuclear Reset):** A single-click routine that deletes custom `DefaultIcon` and `Shell Icons` entries and immediately flushes the cache.

---

### 2.2 [Major / Hardening Required] Explorer File Locking on Central Cache
- **Mechanism:** When Windows Explorer displays an icon referenced in `DefaultIcon`, it memory-maps or opens a read handle on the `.ico` file.
- **Failure Mode:** If the user attempts to change the icon again or restore defaults, overwriting or deleting that `.ico` file throws `System.IO.IOException: The process cannot access the file because it is being used by another process`.
- **Hardening Countermeasure:**
  - Enforce content-addressable storage: `system-icons/{sha256}.ico`. Never overwrite existing files. If an icon is changed, write a new hash file and update the registry pointer. Orphaned icons can be cleaned up during app startup or left as negligible static assets.

---

### 2.3 [Major / Security] UNC Remote SMB/WebDAV Credential Leaks & Injection
- **Mechanism:** In Windows, registry `DefaultIcon` strings accept arbitrary paths and command syntax, including UNC network paths (e.g. `\\attacker-server\share\icon.ico`) and DLL resource indices (`C:\malicious.dll,-1`).
- **Failure Mode:** If an unvalidated path is accepted:
  1. An attacker could craft a rule or profile pointing to a UNC path. When Explorer tries to render the icon, it automatically attempts SMB authentication, leaking the user's NTLMv2 hash over the network.
  2. Pointing to arbitrary DLLs or executables expands security attack surface.
- **Hardening Countermeasure:**
  - Strict input validation whitelist:
    - Path must be a rooted, local drive path (e.g. `C:\...`). Strictly reject UNC paths (`\\...`), relative paths, and URL schemes.
    - File extension must be strictly `.ico` or `.png`. Never allow `.dll`, `.exe`, or script extensions.
    - File must physically exist locally and pass `IcoEncoder` chunk validation.

---

### 2.4 [Major / Hardening Required] Cloud Sync Conflicts (OneDrive / Dropbox Known Folders)
- **Mechanism:** Many Windows 10/11 users have OneDrive "Folder Backup" active, which redirects `Documents`, `Pictures`, and `Desktop` from `%USERPROFILE%\...` to `%USERPROFILE%\OneDrive\...`.
- **Failure Mode:**
  1. Modifying `desktop.ini` inside a OneDrive-synced folder triggers continuous sync loops, conflict copies (`desktop-conflicted-copy.ini`), or OneDrive error badges.
  2. `Environment.GetFolderPath(SpecialFolder.MyDocuments)` points to the OneDrive location, not local disk.
- **Hardening Countermeasure:**
  - Before applying customization to a Known Folder, inspect its path. If it resides under OneDrive or another known cloud sync root, warn the user via an `InfoBar` that cloud synchronization may overwrite or sync `desktop.ini`.
  - Ensure `desktop.ini` is created with `FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM` so cloud sync clients treat it as system metadata.

---

### 2.5 [Minor / Observability] Shell Refresh Storm (`SHCNE_ASSOCCHANGED` Thrashing)
- **Mechanism:** `SHChangeNotify(SHCNE_ASSOCCHANGED)` forces the shell to invalidate all cached associations and icons across every running application. Calling it multiple times in rapid succession (e.g., clicking 4 icon buttons in a row) causes taskbar flickering, temporary UI freezing, and high CPU spikes.
- **Hardening Countermeasure:**
  - Debounce shell notifications in UI dialogs: UI changes update an in-memory staging state or execute registry writes silently, and only broadcast `SHCNE_ASSOCCHANGED` and `SHUpdateRecycleBinIcon()` once when the user clicks "Aplicar" or closes the dialog.

---

## 3. Mitigations & Architecture Hardening Checklist

| Identified Risk | Severity | Hardening Requirement |
|---|---|---|
| Windows 11 Black Box Bug | Critical | Target `HKCU\Software\Classes\Folder\DefaultIcon` first; warn if modifying HKLM `Shell Icons`. |
| Locked `.ico` in Explorer | Major | Content-addressable storage (`sha256.ico`); never overwrite files in-place. |
| Remote UNC NTLM Leaks | Major | Reject any path starting with `\\` or containing network protocols. Enforce local drive validation. |
| Mark-of-the-Web Blocking | Major | Unblock downloaded files (`Zone.Identifier`) before encoding into central cache. |
| Shell Notification Storm | Minor | Debounce `SHCNE_ASSOCCHANGED` and `SHUpdateRecycleBinIcon()` to prevent Explorer freezing. |
| Undo Store Disconnect | Minor | Extend snapshot schema or provide a dedicated "Restaurar todo a fábrica" button in the dialog. |
