# Folder Icon Tool: Stack & UX Alternatives (Stack-Agnostic)

**Date:** 2026-10-03
**Supersedes:** the Python/PySide6 assumption in `windows-folder-icons-automation.md` (the low-level Win32/desktop.ini facts there remain valid).
**Driving constraint:** the tool must make changing folder icons *as easy as possible*. Language/framework is open.

## 1. Existing tools (what "easy" looks like today)

| Tool | Model | Notes |
| :--- | :--- | :--- |
| Folder Painter | Right-click menu, free | Color/icon packs, minimal UI |
| Folder Marker | Right-click menu + app, free/Pro | Batch processing, status icons, Pro has "distributable" folders |
| FolderIco | Right-click menu | Converts custom images to folder icons |
| Rainbow Folders | Legacy | Superseded |
| Native Windows | Properties > Customize > Change Icon | One folder at a time, `.ico` only |
| PowerToys | None | No folder icon feature |

Takeaway: the winning UX pattern is **Explorer right-click integration** (zero context switch), not a standalone window with a folder list. A standalone app is still useful for batch, packs, rules and history.

## 2. Framework options (Windows-only utility, 2026)

| Option | Bundle | Shell/Win32 access | Explorer context menu | Notes |
| :--- | :--- | :--- | :--- | :--- |
| Python + PySide6 | ~45-65 MB | ctypes | Classic menu via registry only | Fast to prototype |
| C# WinUI 3 / WPF (.NET) | Small to moderate | Native P/Invoke | Needs native C++ bridge for Win11 top-level menu | Best Windows fit, Fluent look (WinUI) |
| Tauri (Rust + WebView2) | ~3-10 MB | Rust `windows` crate | Native DLL still needed for top-level menu | Lean, web UI for icon grids |
| Electron | 100+ MB | Node addons | Same limitation | Not justified for this tool |
| PowerShell/AHK scripts | Tiny | Direct | Registry verbs | No real GUI, weak preview/history |

## 3. Explorer context menu constraints (Windows 11)

- Top-level (modern) menu requires `IExplorerCommand` in a **native COM DLL** + package identity (MSIX or Sparse Package). C#/Rust inside explorer.exe is discouraged.
- Legacy registry verbs (`HKCU\Software\Classes\Directory\shell\...`) work without packaging but appear under "Show more options" on Windows 11.
- Pragmatic path: registry verb first (cheap, per-user, no admin), optional sparse-package upgrade later.
- Multi-selection via registry verbs launches one process per item unless using `MultiSelectModel=Player` or a single-instance IPC pattern; must be handled by the app (single-instance + argument aggregation).

## 4. desktop.ini security hardening (June 2026)

- Windows ignores `desktop.ini` icons from untrusted sources: Mark-of-the-Web files, some remote/WebDAV and untrusted network paths. Folder reverts to default icon.
- Implication: network shares and downloaded folders may not show custom icons; the tool should detect and warn instead of reporting false success.
- Relative paths (`IconResource=.\icon.ico,0`) survive folder moves/copies; absolute paths break. Cloud sync (OneDrive/Drive) may overwrite `desktop.ini`.

## 5. Open product decisions (to resolve with the user)

1. Primary interaction: right-click vs standalone window vs both.
2. Stack: .NET (WinUI 3) vs Tauri vs Python.
3. Icon storage: embedded relative copy (portable, pollutes folder) vs central cache (clean, breaks on move).
4. Feature scope per release (MVP vs extensions: packs, rules, history/undo).

## 6. System icons (Recycle Bin, This PC, etc.) - research 2026-10-03

| Target | Mechanism | Scope / privileges | Notes |
| :--- | :--- | :--- | :--- |
| Recycle Bin | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon` | Per user, no admin | Distinct empty/full states (value names `empty` / `full` reported; confirm in spike). Refresh with `SHChangeNotify(SHCNE_ASSOCCHANGED)`. |
| This PC | same pattern, CLSID `{20D04FE0-3AEA-1069-A2D8-08002B30309D}` | Per user | |
| User's Files | CLSID `{59031a47-3f72-44a7-89c5-5595fe6b30ee}` | Per user | |
| Network | CLSID `{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}` | Per user | |
| Control Panel | CLSID `{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}` | Per user | |
| Drives | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons\<letter>\DefaultIcon` | **Admin required** | Removable media: `autorun.inf` `icon=` (portable) |
| File types | `HKCU\Software\Classes\<ProgID>\DefaultIcon` | Per user override possible | Needs shell refresh; Windows updates/apps can reset |
| Known folders (Documents, Downloads) | CLSID DefaultIcon or `desktop.ini` | Restricted | Less stable; may be overridden by updates |

Design implications:
- The registry is not `desktop.ini`: needs a separate backup/undo path (store previous `DefaultIcon` values; deleting the override restores the default).
- Icon file must live at a stable absolute path (registry has no relative paths): central storage in `%LOCALAPPDATA%` is the right mode for system icons.
- Per-user (HKCU) targets need no elevation; drives need admin and should be a later, optional item.
- Windows Personalization > Desktop icon settings already offers a manual per-icon picker; value of this tool is `.png` conversion, one-step apply, and undo.
