# Repository Agent Guidelines: Windows Icons Admin

This document defines architecture invariants, development workflows, quality gates, and release packaging rules for **Windows Icons Admin**. All AI agents and automated workflows interacting with this repository must strictly adhere to these directives.

---

## 1. Project Overview & Architecture

**Windows Icons Admin** is a modern Windows 11 desktop application engineered with .NET 9 WinUI 3 (Windows App SDK) and Cleanroom Software Engineering principles. It automates Windows folder icon customization, rule-based matching, multi-resolution chunk-verified ICO generation, and transactional undo/redo without disrupting the Windows Shell.

### Clean Architecture & Modularity
- **`WindowsIconsAdmin.Core`**: Target-independent class library containing 100% pure domain logic without platform or UI dependencies:
  - `Rules/`: Pattern-matching engine (`FolderRule`, `RuleEngine`) with ReDoS protection.
  - `Imaging/`: In-memory PNG-to-ICO multi-resolution conversion (`IcoEncoder`) with 7-layer DIB + PNG256 encoding.
  - `History/`: Thread-safe, atomic undo/redo transaction log (`UndoStore`).
  - `Safety/`: System folder protection and traversal guards (`SystemFolderGuard`).
  - `Shell/`: Hardened `desktop.ini` parsers and shell notification services.
- **`WindowsIconsAdmin.App`**: Unpackaged WinUI 3 desktop application featuring Windows 11 Fluent Design and Mica backdrop.
- **`tests/WindowsIconsAdmin.Core.Tests`**: Cleanroom unit test suite validating contracts, regex timeouts, binary ICO structures, and concurrency.
- **`installer/setup.iss`**: Inno Setup packaging script producing the official Setup executable.

---

## 2. Release Distribution Invariant (Single Deliverable Policy)

> [!IMPORTANT]
> **Single Official Deliverable for GitHub Releases:**
> Every release published on GitHub **MUST ALWAYS** publish **ONLY** the official Inno Setup installer executable:
> ```text
> WindowsIconsAdmin_Setup.exe
> ```
> *(or `WindowsIconsAdmin_Setup-vX.Y.Z.exe` if versioned by the build script).*
> - **Strict Prohibition on Extra Assets**: NEVER publish standalone portable executables (`.exe`), unpackaged binaries (`dist/win-x64/*`), or compressed archives (`.zip`) as GitHub Release assets. Production binaries compiled during `dotnet publish` exist strictly to be packaged into the Setup installer by Inno Setup, never as independent downloads.
> - **Verification Gate**: Any release workflow or release agent (`ami-release-manager`) must verify with `gh release view <tag> --json assets` that only the Setup installer executable is uploaded. Releases containing portable/zip files or lacking the Setup executable are strictly non-compliant.

### Packaging Pipeline Specification
1. **Version Bump Order**: Bump the version in:
   - `installer/setup.iss` (`#define MyAppVersion "X.Y.Z"`)
   - `src/WindowsIconsAdmin.App/WindowsIconsAdmin.App.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`)
2. **Publish Self-Contained Deployment**:
   ```powershell
   dotnet publish src/WindowsIconsAdmin.App/WindowsIconsAdmin.App.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
   ```
3. **Compile Inno Setup Installer**:
   ```powershell
   & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/setup.iss
   ```
   *Target output: `releases/WindowsIconsAdmin_Setup.exe`.*
4. **Release Asset Upload**:
   ```powershell
   gh release upload vX.Y.Z "releases/WindowsIconsAdmin_Setup.exe" --clobber
   ```
5. **Local Cleanliness**: Purge local `releases/` directory post-upload to avoid disk clutter.

---

## 3. Mandatory Agent Rules & Directives

### 🌐 Language & Communication
- **Source Code**: All C# code (classes, methods, variables, docstrings) MUST be in **English**.
- **User Chat**: Communicate with the user in **Spanish** unless requested otherwise.
- **Git Commits**: Use **Conventional Commits** in **English** (`feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`).
- **README Files**: Maintain symmetric bilingual documentation (`README.md` and `README.es.md`).
- **Zero Flags Rule**: Never use country flag emojis (`🇺🇸`, `🇪🇸`, etc.) in documentation or UI.

### 🔒 Security & Privacy
- **Absolute Paths**: NEVER leak absolute computer paths (e.g., `C:\Users\...`) into code, documentation, or commits. Always use relative paths or generic placeholders.
- **System Folder Guard**: Critical Windows system folders (`C:\Windows`, `%SystemRoot%`, `%ProgramFiles%`) must remain strictly protected against attribute modifications or `desktop.ini` injection.

### 💻 PowerShell Environment
- **Command Chaining**: NEVER use `&&` or `||` in terminal commands. Use `;` or separate sequential commands.
- **GitHub CLI Context**: Switch to personal account `AnaCataVC` (`gh auth switch -u AnaCataVC --hostname github.com 2>$null`).

---

## 4. Windows Shell & WinUI 3 Stability Invariants

1. **Win32 Folder Attribute Shell Gate**: Windows Explorer bypasses `desktop.ini` unless the directory is flagged with `FILE_ATTRIBUTE_READONLY` (`0x0001`) or `FILE_ATTRIBUTE_SYSTEM` (`0x0004`). On folders, this attribute acts purely as an OS shell signal and does not lock file writing.
2. **Non-Destructive Shell Refresh**: Never terminate `explorer.exe`. Combine `SHCNE_UPDATEITEM` (per-path) with `SHCNE_ASSOCCHANGED` (global icon cache flush) to achieve instant live updates without destroying taskbar state.
3. **DispatcherQueue Ordering**: Initialize and capture `App.DispatcherQueue` prior to instantiating `MainWindow` to prevent null references during early ViewModel resolution.
4. **Crash Logging**: Global exception handlers must persist full diagnostics to `%LOCALAPPDATA%\WindowsIconsAdmin\Logs\crash.log`.

---

## 5. Build & Test Commands (PowerShell)

```powershell
# Restore & build complete solution
dotnet build WindowsIconsAdmin.sln -c Release

# Execute Cleanroom test suite (188+ tests)
dotnet test tests/WindowsIconsAdmin.Core.Tests/WindowsIconsAdmin.Core.Tests.csproj
```
