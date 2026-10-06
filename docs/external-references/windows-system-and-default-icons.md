> **Created:** 2026-10-04
> **Last Updated:** 2026-10-06

# Windows System, Default Folder, and File Type Icons Architecture & Registry Reference

## 1. Executive Summary
Windows Shell organizes icons across several distinct subsystems:
1. **Desktop / Shell CLSID Icons**: Virtual shell objects (Recycle Bin, This PC, Network, User Files, Home, Gallery, Linux WSL) defined by CLSID in Registry (see also [Windows 11 Navigation Pane & Drive Icon Customization Reference](windows11-navigation-pane-and-drive-icons.md)).
2. **Special User Folders (Known Folders)**: Physical filesystem directories (Desktop, Documents, Downloads, Pictures, Music, Videos) customized via `desktop.ini` or KnownFolder GUIDs.
3. **Generic Default Folder Icons**: The standard closed/open folder icon used by Windows Explorer for uncustomized directories.
4. **Generic Unknown File Icon**: The fallback icon shown for unregistered or unknown file extensions.
5. **Extension-Specific File Type Icons**: The visual icon associated with a specific file extension (e.g. `.txt`, `.pdf`, `.mp4`).

---

## 2. Desktop & Shell CLSID Icons (Recycle Bin, This PC, Network, User Files, Navigation Pane)

### Registry Locations
- **User CLSID Overrides (Desktop & Shell)**:
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{CLSID}\DefaultIcon`
- **Per-User HKCR Merged CLSID Overrides (Navigation Pane)**:
  `HKEY_CURRENT_USER\Software\Classes\CLSID\{CLSID}\DefaultIcon`
- **Windows Themes Overrides** (Windows 10 / 11 Themes sync & override):
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\DefaultIcon`

### Key CLSIDs & Values
| Shell Object | CLSID | Registry Values |
|---|---|---|
| **Recycle Bin** | `{645FF040-5081-101B-9F08-00AA002F954E}` | `(Default)`, `empty`, `full` |
| **This PC** | `{20D04FE0-3AEA-1069-A2D8-08002B30309D}` | `(Default)` |
| **Network** | `{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}` | `(Default)` |
| **User Files** | `{59031a47-3f72-44a7-89c5-5595fe6b30ee}` | `(Default)` |
| **Home (`Inicio`)** | `{f874310e-b6b7-47dc-bc84-b9e6b38f5903}` | `(Default)` |
| **Gallery (`Galería`)** | `{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}` | `(Default)` |
| **Linux (WSL)** | `{B2B4A4D1-2754-4140-A2EB-9A76D9D7CDC6}` | `(Default)` |

### Critical Quirks & Requirements:
1. **Icon Path Formatting**: The path stored in `DefaultIcon` registry keys **MUST include the resource index suffix** (e.g., `C:\Path\icon.ico,0`). If `,0` is stripped, Windows Explorer often fails to resolve or render the icon.
2. **Recycle Bin Dual State**: When updating the Recycle Bin, both `(Default)` and `empty`/`full` must be maintained synchronously.
3. **P/Invoke `SHUpdateRecycleBinIcon`**: Calling `SHChangeNotify(SHCNE_ASSOCCHANGED)` alone does NOT invalidate the internal shell recycle bin state in Explorer. Win32 provides an exported function in `shell32.dll`:
   ```csharp
   [DllImport("shell32.dll", EntryPoint = "SHUpdateRecycleBinIcon")]
   public static extern void SHUpdateRecycleBinIcon();
   ```
   Invoking this immediately triggers Windows Explorer to refresh the Recycle Bin desktop glyph.

---

## 3. Generic Default Folder Icon (Windows Global Closed & Open Folders)

### Registry Mechanisms:
- **Per-User (HKCU - No Administrator Privileges Required)**:
  `HKEY_CURRENT_USER\Software\Classes\Folder\DefaultIcon`
  - Value: `(Default)` = `"C:\Path\icon.ico,0"`
  - Overrides the default closed folder icon for all folders without a custom `desktop.ini` for the current user.
- **Machine-Wide (HKLM - Requires UAC Elevation)**:
  `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons`
  (and `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons` on 64-bit OS)
  - String Value `"3"` = Closed Folder Icon
  - String Value `"4"` = Open Folder Icon
  - Setting `"3"` and `"4"` to `"C:\Path\icon.ico,0"` sets the system-wide folder icon.
  - Deleting the `"3"` and `"4"` values restores the default Windows imageres.dll / shell32.dll folder icon.

---

## 4. Generic Unknown File Icon

### Registry Mechanisms:
- **Per-User (HKCU - No Administrator Privileges Required)**:
  `HKEY_CURRENT_USER\Software\Classes\Unknown\DefaultIcon`
  - Value: `(Default)` = `"C:\Path\icon.ico,0"`
- **Machine-Wide (HKLM - Requires UAC Elevation)**:
  `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons`
  - String Value `"0"` = Generic / Unknown Document Icon

---

## 5. File Extension Specific Icons (.txt, .pdf, etc.)

In Windows 10/11, file extensions are mapped to ProgIDs:
1. **Resolution Sequence**:
   - Check `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ext\UserChoice` -> `ProgId` value.
   - If absent, check `HKEY_CURRENT_USER\Software\Classes\.ext` -> `(Default)`.
   - If absent, check `HKEY_LOCAL_MACHINE\SOFTWARE\Classes\.ext` -> `(Default)`.
2. **Per-User Icon Override**:
   - If a ProgID is resolved (e.g. `txtfile` or `AppX...`), set:
     `HKEY_CURRENT_USER\Software\Classes\<ProgId>\DefaultIcon` -> `(Default)` = `"C:\Path\icon.ico,0"`.
   - In addition, Windows supports extension fallback association:
     `HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\.ext\DefaultIcon` -> `(Default)` = `"C:\Path\icon.ico,0"`.
3. **Invalidation**:
   Execute `SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero)`.

---

## 6. User Profile Special Folders (Known Folders)

Special folders (Desktop, Documents, Downloads, Music, Pictures, Videos):
- Resolved via `Environment.GetFolderPath(SpecialFolder)` or `SHGetKnownFolderPath`.
- Customized directly via `desktop.ini` using `ApplyFolderIcon` with `IconStorageMode.PortableEmbedded` or `IconStorageMode.CentralCache`.
- Shell attribute invariants: folder must have `FILE_ATTRIBUTE_READONLY` set, `desktop.ini` must have `FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM`.

---

## 7. Windows Shell Cache Invalidation Protocol

To guarantee changes appear immediately without restarting `explorer.exe`:
1. Call `SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero)`.
2. Call `SHUpdateRecycleBinIcon()` for recycle bin changes.
3. Broadcast `WM_SETTINGCHANGE` with `lParam = "ShellState"` or `SPI_SETNONCLIENTMETRICS` to desktop window when updating shell icons.
