# Windows Folder Icon Automation & Shell Integration Research

**Document Slug:** `windows-folder-icons-automation`  
**Date:** 2026-10-03  
**Target Environment:** Windows 10 / Windows 11 (64-bit), Python 3.11+

---

## 1. Executive Summary

This document synthesizes technical requirements, Win32 API specifications, filesystem attributes, image conversion standards, and GUI architectural trade-offs for building a bulk Windows folder icon customizer in Python.

---

## 2. Windows Shell Customization Mechanisms

Windows Explorer determines whether a folder contains custom metadata (such as custom icons, localized names, or custom views) by inspecting specific filesystem attributes before parsing any metadata files.

### 2.1 The `desktop.ini` Protocol
When Windows Explorer accesses a folder:
1. It queries the folder's file system attributes.
2. If the folder has either the `FILE_ATTRIBUTE_READONLY` (`0x0001`) or `FILE_ATTRIBUTE_SYSTEM` (`0x0004`) flag set, Explorer looks for a file named `desktop.ini` in the folder root.
3. If neither flag is set, Explorer **skips** `desktop.ini` entirely for performance reasons.

### 2.2 Structure of `desktop.ini`
The configuration file requires the `[.ShellClassInfo]` section:
```ini
[.ShellClassInfo]
IconResource=C:\path\to\icon.ico,0
[ViewState]
Mode=
Vid=
FolderType=Generic
```
- `IconResource`: Path to the `.ico` (or `.dll`/`.exe`) followed by `,0` representing the icon index.
- If referencing an icon placed inside the directory itself, relative pathing (`IconResource=icon.ico,0`) or absolute pathing can be used. Absolute paths ensure maximum compatibility across varying Windows Shell versions.

### 2.3 Required File Attributes
Using the Win32 API `kernel32.SetFileAttributesW`:
- **Target Folder:** Must be set to `FILE_ATTRIBUTE_READONLY` (`0x0001`) or `FILE_ATTRIBUTE_SYSTEM` (`0x0004`). In modern Windows (Win 10/11), `FILE_ATTRIBUTE_READONLY` on directories has no effect on user file creation/deletion, acting solely as a shell flag.
- **`desktop.ini` File:** Must be set to `FILE_ATTRIBUTE_HIDDEN` (`0x0002`) | `FILE_ATTRIBUTE_SYSTEM` (`0x0004`).

### 2.4 Restoring Default Folder Icons
To revert a folder to its default state:
1. `desktop.ini` must have its attributes reset to `FILE_ATTRIBUTE_NORMAL` (`0x0080`) before deletion; otherwise, `os.remove()` may fail with `WinError 5: Access is Denied`.
2. Delete `desktop.ini` (or remove `[.ShellClassInfo]` if other shell extensions are present).
3. If no other custom attributes are needed, reset the folder attributes to `FILE_ATTRIBUTE_NORMAL`.
4. Trigger shell notifications to immediately refresh Explorer.

### 2.5 Cache Invalidation & Real-Time Shell Refresh
Without notification, Windows Explorer caches icon references, causing delay or requiring a process restart (`Stop-Process -Name explorer -Force`).
Using `shell32.SHChangeNotify`:
```python
import ctypes
from ctypes import wintypes

SHCNE_ASSOCCHANGED = 0x08000000
SHCNE_UPDATEITEM   = 0x00002000
SHCNF_IDLIST       = 0x0000
SHCNF_PATHW        = 0x0005

# Specific folder item update
ctypes.windll.shell32.SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, folder_path, None)

# Broad shell association update (forces Explorer icon reload)
ctypes.windll.shell32.SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, None, None)
```

---

## 3. Icon Generation & Format Standards (.ico)

### 3.1 Multi-Resolution Requirements
Windows Explorer renders folder icons across various view modes:
- Extra Large Icons: 256x256
- Large Icons: 96x96 or 128x128
- Medium Icons: 48x48
- Small / Details / List: 16x16, 24x24, 32x32

### 3.2 Win32 DIB vs PNG Compression
- Standard sizes (16x16, 24x24, 32x32, 48x48, 64x64, 128x128) must be formatted with raw uncompressed 32-bit DIB bitmaps (`BITMAPINFOHEADER` + BGRA).
- The 256x256 layer should use PNG compression to prevent file bloat while maintaining clarity.

### 3.3 Conversion Implementation with Pillow
```python
from PIL import Image

def convert_png_to_ico(input_png_path: str, output_ico_path: str) -> None:
    with Image.open(input_png_path) as img:
        img = img.convert("RGBA")
        sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
        img.save(
            output_ico_path,
            format="ICO",
            sizes=sizes,
            bitmap_format="bmp"
        )
```

---

## 4. Icon Storage Strategy

| Strategy | Pros | Cons | Recommendation |
| :--- | :--- | :--- | :--- |
| **A. Absolute External Reference** (`IconResource=C:\path\icon.ico,0`) | No extra files inside target folder. Clean directory structure. | If user moves, renames, or deletes the icon file, the folder icon breaks. | High fragility |
| **B. Local Embedded Copy** (Copies `.folder_icon.ico` into target folder marked Hidden/System) | **Completely portable.** Moving folder or changing drive letters preserves the icon. Independent of external files. | Adds one hidden file inside the folder. | **Recommended Default** (with option for external link) |

---

## 5. GUI Framework Evaluation

| Criteria | PyQt6 / PySide6 | CustomTkinter | Standard Tkinter + ttk |
| :--- | :--- | :--- | :--- |
| **UI Modernity** | Native Fluent/Dark modern UI, high DPI crispness | Modern dark/light theme, rounded cards | Basic legacy look unless customized |
| **Multi-folder Selection** | Easy via QFileDialog, custom dialog, and native drag-and-drop (`dragEnterEvent`) | Requires extra hooks or single folder pickers | Standard `askdirectory` only supports 1 folder at a time |
| **Performance & Threading** | Robust `QThread` and `pyqtSignal` architecture | Threading with `queue.Queue` or `after()` | Threading with `queue.Queue` or `after()` |
| **Distribution / Bundle Size** | ~35-50 MB PyInstaller bundle | ~15-20 MB bundle | Smallest (~10 MB) |
| **Thumbnail & Icon Grid** | Native `QListWidget` / `QTableWidget` icon mode with smooth scaling | Canvas / label grids (slower rendering) | Canvas / label grids |

**Recommendation:**
- **Option 1 (PyQt6 / PySide6):** Offers the most modern Windows 11 Fluent interface, native drag-and-drop from Windows Explorer, seamless icon pack grid previews, and rock-solid threading.
- **Option 2 (CustomTkinter + pywin32):** Lightweight, dark-themed, no heavy Qt dependencies, with custom multi-folder selector and preview cards.

---

## 6. Architecture & State Management

### 6.1 Transactional History & Undo
A local transaction log (`data/history.json` or user appdata) tracks every batch operation:
```json
{
  "batch_id": "uuid4",
  "timestamp": "2026-10-03T17:30:00Z",
  "action": "apply_icon",
  "icon_path": "C:\\path\\icon.ico",
  "folders": [
    {
      "path": "C:\\Projects\\App1",
      "previous_state": {
        "had_desktop_ini": true,
        "backup_desktop_ini": "[.ShellClassInfo]\n...",
        "previous_attributes": 1
      }
    }
  ]
}
```
This enables full, deterministic rollback for the "Revertir último cambio" action.

### 6.2 Automation Rule Engine
Rules defined by simple declarative contracts:
- Condition: `contains`, `starts_with`, `ends_with`, `regex`
- Target pattern: string or regex
- Target icon path: `.ico` or `.png`
- Recursive evaluation option for subdirectories
