<p align="center">
  <img src="icon.png" alt="WindowsIconsAdmin Logo" width="120" />
</p>

# WindowsIconsAdmin

[English](README.md) | [Español](README.es.md)

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3.0-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Platform Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![Tests Passing](https://img.shields.io/badge/Tests-459%20passing-brightgreen?logo=xunit)](tests/WindowsIconsAdmin.Core.Tests)
[![Architecture](https://img.shields.io/badge/Architecture-Cleanroom%20TDD-orange)](docs/adr/0001-winui3-dotnet-architecture.md)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

High-performance, automated bulk folder icon administration utility for Windows 10 and 11, engineered in C# 13 and .NET 9 with strict Cleanroom TDD standards.

---

## Overview

Customizing folder icons in Windows has historically been divided between manual, single-folder property sheets or abandoned legacy utilities. WindowsIconsAdmin modernizes this workflow into an automated, non-destructive desktop application that respects developer workspaces, cloud sync trees, and Windows Shell integrity.

### Key Features

- **Automated Rule Engine**: High-throughput rule matching (`Contains`, `StartsWith`, `EndsWith`, `Regex`) with priority ordering and ReDoS catastrophic backtracking protection.
- **Pure-Stream ICO Encoding**: Generates compliant 7-resolution `.ico` binaries (16px to 256px) from arbitrary PNGs with chunk-level integrity verification.
- **Central Cache & Dual Storage Modality**: Enforces Central Cache (`%LOCALAPPDATA%\WindowsIconsAdmin\Icons\`) by default to keep folders 100% clean without loose icon assets. Portable embedded mode (`.folder_icon.ico`) is supported as an opt-in advanced setting for removable USB drives.
- **Windows 11 Navigation Pane & Drive Icons**: Customizable icons for Windows 11 Explorer sidebar items and drive volumes with safe OneDrive restoration safeguards.
- **Persistent Configuration & Settings Dialog**: Thread-safe configuration system via `AppSettingsService` (`settings.json`) paired with a dedicated modal `SettingsDialog` accessible from the top toolbar, featuring automated global `.gitignore` integration for `desktop.ini`.
- **Deterministic Transactional Undo**: Multi-batch snapshot history with atomic file persistence and automatic corruption recovery.
- **Zero-Disruption Shell Invalidation**: Real-time icon refresh via dual-stage `SHChangeNotify` Win32 notifications without restarting `explorer.exe`.

---

## Clean Architecture

The solution enforces strict separation of concerns, decoupling pure domain algorithms from Windows Shell APIs and presentation layers.

```mermaid
flowchart TD
    subgraph UI ["Presentation Layer"]
        App["WindowsIconsAdmin.App (WinUI 3)<br/>(MainPage & SettingsDialog)"]
    end

    subgraph Shell ["Platform Services"]
        ShellSvc["WindowsIconsAdmin.ShellServices<br/>(Win32 P/Invoke & desktop.ini)"]
    end

    subgraph Core ["Core Domain (Zero Dependencies)"]
        Rules["RuleEngine<br/>(Pattern Matching & ReDoS Safe)"]
        Imaging["IcoEncoder<br/>(7-Layer DIB + PNG256)"]
        History["UndoStore<br/>(Transactional JSON + Atomic Write)"]
        Storage["IconStorageService<br/>(CentralCache & Portable Modes)"]
        Settings["AppSettingsService<br/>(Persistent JSON Configuration)"]
        Safety["SystemFolderGuard<br/>(System & Known Folder Protection)"]
    end

    App --> ShellSvc
    App --> Core
    ShellSvc --> Core
```

### Module Responsibilities

- **`WindowsIconsAdmin.Core`**: Target-independent class library containing pure logic:
  - `Rules`: High-throughput folder name rule evaluation (`FolderRule`, `RuleEngine`).
  - `Imaging`: In-memory PNG-to-ICO multi-resolution conversion (`IcoEncoder`).
  - `History`: Thread-safe, atomic undo/redo transaction log (`UndoStore`).
  - `Storage`: Storage engine managing Central Cache (`%LOCALAPPDATA%`) and optional Portable Mode (`IconStorageService`, `IconStorageMode`).
  - `Settings`: Thread-safe persistent configuration store (`AppSettings`, `AppSettingsService`).
  - `Safety`: System folder protection, path traversal defenses, and restricted file extension guards (`SystemFolderGuard`).
- **`WindowsIconsAdmin.ShellServices`**: Encapsulates Win32 filesystem attributes, `desktop.ini` generation, and shell cache refresh.
- **`WindowsIconsAdmin.App`**: Modern Windows 11 Fluent UI application (WinUI 3 / Windows App SDK) featuring multi-folder drag-and-drop, batch progress monitoring, rule management, and a comprehensive `SettingsDialog`.
- **`installer/`**: Official Inno Setup 6 packaging producing the single signed Setup deliverable (`WindowsIconsAdmin_Setup.exe`).

---

## Tech Stack

- **Runtime & Language**: .NET 9.0 (`net9.0-windows10.0.26100.0`), C# 13.0
- **UI Framework & Presentation**: WinUI 3 (Windows App SDK 1.6), Windows 11 Fluent Design & Mica material
- **Platform Interop**: Win32 Shell API (`shell32.dll`, `SHChangeNotify`, `GetFileAttributesW`, `SetFileAttributesW`)
- **Packaging & Distribution**: Inno Setup 6 (lzma2/ultra64 solid compression)
- **Unit Testing Framework**: xUnit 2.9, FluentAssertions 8.0

---

## Getting Started

### Prerequisites

- [Windows 10 (Build 19041+) or Windows 11](https://www.microsoft.com/windows)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Local Setup & Build

Clone the repository and build the complete solution:

```powershell
# Clone repository
git clone https://github.com/AnaCataVC/windows-icons-admin.git
cd windows-icons-admin

# Restore and build solution
dotnet build WindowsIconsAdmin.sln -c Release
```

---

## Automated Testing

WindowsIconsAdmin.Core was engineered using a rigorous **Cleanroom Software Engineering** approach:

1. **Frozen Interface Contract**: Complete API specifications, boundary conditions, and acceptance criteria were formalized in [core-services.contract.md](docs/contracts/core-services.contract.md), [safety-and-integrity.contract.md](docs/contracts/safety-and-integrity.contract.md), and [window-sizing.contract.md](docs/contracts/window-sizing.contract.md) before writing code.
2. **Air-Gapped Test & Implementation**: Unit test suites were developed against the specification independently from the core implementation.
3. **Comprehensive Verification**: 459 unit tests validate error boundaries, thread safety, regex catastrophic timeouts, binary ICO structures, system folder protections, and persistent configuration durability.

```powershell
# Execute complete Cleanroom test suite
dotnet test tests/WindowsIconsAdmin.Core.Tests/WindowsIconsAdmin.Core.Tests.csproj
```

```text
Total Tests:     459
Passed:          459 (100%)
Failed:            0
Skipped:           0
Duration:        ~2.0s
```

---

## Key Learnings & Architectural Decisions

Building and architecting WindowsIconsAdmin under Cleanroom TDD standards yielded fundamental Windows systems engineering takeaways:

1. **Win32 Folder Attribute Shell Gate:** Windows Explorer ignores `desktop.ini` unless the directory is flagged with `FILE_ATTRIBUTE_READONLY` (`0x0001`) or `FILE_ATTRIBUTE_SYSTEM` (`0x0004`). On folders, this attribute does not lock write permissions and acts solely as an internal shell signal to evaluate customizations.
2. **Central Cache Policy & Workspace Cleanliness:** Writing embedded `.folder_icon.ico` files pollutes directory trees and Git repositories. Enforcing `IconStorageMode.CentralCache` as the default storage mechanism in `%LOCALAPPDATA%` leaves folders clean while retaining instant icon resolution. Portable mode is preserved as an optional, opt-in feature for removable drives.
3. **Persistent Atomic Configuration (`AppSettingsService`):** Configuration state is maintained in `%LOCALAPPDATA%\WindowsIconsAdmin\settings.json` using thread-safe synchronization gates, atomic file replacement (`.tmp` to target via `File.Replace`), and automatic corrupted-file backup (`.bak`).
4. **Non-Destructive Shell Refresh:** Terminating `explorer.exe` disrupts taskbar state and running tray apps. Instant live updates are achieved via dual Win32 notifications: `SHCNE_UPDATEITEM` for path-specific updates paired with `SHCNE_ASSOCCHANGED` to invalidate the in-memory shell icon cache.
5. **GDI+ Silent PNG Truncation Guard:** Standard .NET image decoders silently process truncated PNG streams lacking valid `IEND` footer chunks. A custom chunk-level PNG integrity parser was built to prevent generating malformed `.ico` binaries.
6. **Windows 11 Context Menu Separation:** Modern top-level context menus require an in-process native C++ COM DLL implementing `IExplorerCommand` packaged with identity. Classic registry verbs (`HKCU\Software\Classes\Directory\shell`) provide instant, unprivileged integration under "Show more options".
7. **Security Hardening (Mark-of-the-Web):** Windows Explorer ignores custom icons on directories marked with NTFS Zone 3 (Internet) identifiers to mitigate remote UNC attacks and credential leak vectors.
8. **Cleanroom TDD Architectural Decoupling:** `WindowsIconsAdmin.Core` was engineered with zero dependencies on UI or platform APIs, enforcing frozen interface contracts and comprehensive air-gapped unit tests covering ReDoS timeouts, thread-safe undo transactions, multi-resolution binary encoding, and system folder protection safeguards.

For deep technical analysis, read:
- [Windows Shell & Icon Engineering Quirks](docs/learning/windows-shell-and-icon-quirks.md)
- [ADR 0001: .NET 9 WinUI 3 Architecture & Cleanroom TDD](docs/adr/0001-winui3-dotnet-architecture.md)
- [ADR 0002: System Folder Guard & INI Hardening](docs/adr/0002-system-folder-guard-and-ini-hardening.md)
- [ADR 0003: Multi-Folder Picker & Persistent Rules Engine](docs/adr/0003-multi-folder-picker-and-persistent-rules-engine.md)

---

## Repository Structure

```
windows-icons-admin/
├── docs/
│   ├── adr/
│   │   ├── 0001-winui3-dotnet-architecture.md
│   │   ├── 0002-system-folder-guard-and-ini-hardening.md
│   │   └── 0003-multi-folder-picker-and-persistent-rules-engine.md
│   ├── audits/
│   │   └── initial-core-audit.md
│   ├── contracts/
│   │   ├── core-services.contract.md
│   │   ├── safety-and-integrity.contract.md
│   │   └── window-sizing.contract.md
│   ├── external-references/
│   │   ├── folder-icon-tool-stack-alternatives.md
│   │   ├── system-icons-stress-test.md
│   │   ├── windows-folder-icons-automation.md
│   │   ├── windows-system-and-default-icons.md
│   │   └── windows11-navigation-pane-and-drive-icons.md
│   └── learning/
│       ├── windows-shell-and-icon-quirks.md
│       └── windows-shell-safety-and-integrity.md
├── installer/
│   └── setup.iss          # Inno Setup packaging script
├── src/
│   ├── WindowsIconsAdmin.App/
│   │   ├── Dialogs/       # SettingsDialog, RulesDialog, SystemIconsDialog
│   │   ├── Services/      # WinUI 3 dialog and picker helpers
│   │   ├── ViewModels/    # MVVM models and folder item tracking
│   │   └── Views/         # WinUI 3 pages and window controls
│   └── WindowsIconsAdmin.Core/
│       ├── History/       # Transactional undo and rolling snapshot store
│       ├── Imaging/       # Chunk-verified multi-resolution ICO encoder
│       ├── Layout/        # Window sizing and DPI computation
│       ├── Rules/         # ReDoS-safe rule matching engine
│       ├── Safety/        # System folder protection & traversal guard
│       ├── Settings/      # Persistent AppSettings & thread-safe AppSettingsService
│       ├── Shell/         # Hardened INI helpers and shell services
│       └── Storage/       # Central cache and portable icon storage service
├── tests/
│   └── WindowsIconsAdmin.Core.Tests/
│       ├── Helpers/       # Test PNG and ICO binary validators
│       ├── History/       # UndoStore concurrency and durability tests
│       ├── Imaging/       # IcoEncoder pixel and chunk boundary tests
│       ├── Rules/         # RuleEngine regex and condition tests
│       ├── Safety/        # SystemFolderGuard and path traversal tests
│       ├── Settings/      # AppSettingsService persistence and corruption recovery tests
│       ├── Shell/         # Hardened IniHelper and ShellIconService tests
│       └── Storage/       # IconStorageService and path validation tests
├── README.md
├── README.es.md
└── WindowsIconsAdmin.sln
```

---

## License

This project is licensed under the [MIT License](LICENSE).
