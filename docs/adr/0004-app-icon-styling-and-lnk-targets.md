# ADR 0004: App Icon Styling Architecture, Deterministic Pixel Pipeline, and Shortcut (.lnk) Target Scope

- **Status:** Accepted
- **Date:** 2026-11-07
- **Deciders:** Core Engineering Team, Architecture Auditor
- **Consulted:** `docs/research/app-icon-targets.md`, `windows-icons-admin-app-icons-plan.md`

---

## 1. Context and Problem Statement

Windows Icons Admin currently manages Windows Explorer folder icons, drives, and shell namespace items. Expanding the application to support **Installed Application Icons** introduces new technical boundaries:
1. Applications are not directories; modifying them cannot rely on `desktop.ini`.
2. Direct binary modification of executable files (`.exe` / `.dll` PE resources) corrupts Authenticode signatures, triggers antivirus detection, and violates the project's non-destructive invariant.
3. Windows renders application icons across heterogeneous surfaces (Start Menu, Desktop, Taskbar, Task Switcher) sourced from different mechanisms.
4. AI-generated icons in runtime introduce non-determinism, network latency, external dependencies, and privacy risks.

We need a deterministic, reversible, local-first architecture for discovering applications, normalizing their visual weight, applying styling containers, and updating Windows Shell shortcuts safely.

---

## 2. Alternatives Considered

### Target Mechanism
| Dimension | Option A: Executable PE Reshacker | Option B: Runtime Shell Hooking | Option C: Shell Shortcuts (.lnk) (Selected) |
| :--- | :--- | :--- | :--- |
| **Safety** | High risk of binary corruption | Process instability / crash risk | **Zero binary modification** |
| **Signature Preservation** | Breaks Authenticode code signing | N/A | **Completely preserves binary signatures** |
| **Permissions Required** | Administrator / TrustedInstaller | Administrator + Kernel/DLL injection | **Standard user (for user shortcuts)** |
| **Reversibility** | Requires binary file backups | Volatile | **100% atomic restoration via IShellLink** |

### Rendering Architecture
| Dimension | Option A: External ImageMagick / Skia CLI | Option B: GDI+ Win32 coupling | Option C: Pure Domain RgbaCanvas Engine (Selected) |
| :--- | :--- | :--- | :--- |
| **Dependencies** | External binaries (~30MB) | Coupled to Windows GDI+ subsystem | **Zero external dependencies, pure C# memory math** |
| **Determinism** | Platform-dependent rounding | GDI+ antialiasing quirks | **100% bitwise deterministic across builds** |
| **Testability** | Heavy integration tests | Requires Windows host | **Runs in-memory and passes cross-platform unit tests** |

---

## 3. Decision

We adopt a **pure domain styling engine** coupled with **IShellLink shortcut targets**:

### 3.1 Two-Assembly Structure Retained
In accordance with ADR 0001, we preserve the existing two-project architecture (`WindowsIconsAdmin.Core` and `WindowsIconsAdmin.App`) without adding third-party assemblies:
- `WindowsIconsAdmin.Core.Apps`: Discovery contracts, `InstalledApp`, `IconTarget`, `AppSupportStatus`.
- `WindowsIconsAdmin.Core.Styling`: `IconStyle`, `ContainerSpec`, `TreatmentSpec`, `RgbaCanvas`, `IconNormalizer`, `IconStyleRenderer`, `RenderedIconCache`.
- `WindowsIconsAdmin.Core.History`: Dedicated `AppIconUndoStore` (`State/app-undo.json`) with identical atomic `.tmp` + `File.Replace` semantics.
- `WindowsIconsAdmin.Core.Shell.Apps`: `IShellLinkW` COM interop, shortcut icon path redirection, and `SHChangeNotify` notifications.

### 3.2 Target Scope Policy (Cleanroom Invariant)
1. **Fully Supported (`✓`)**:
   - User Start Menu shortcuts: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\**\*.lnk`
   - User Desktop shortcuts: `%USERPROFILE%\Desktop\*.lnk`
2. **Partially Supported (`△`)**:
   - Machine-wide shortcuts: `%ProgramData%\Microsoft\Windows\Start Menu\Programs\**\*.lnk` and `%PUBLIC%\Desktop\*.lnk` (cataloged, but flagged as requiring elevated privileges).
3. **Explicitly Unsupported (`✕`)**:
   - Packaged apps (MSIX / UWP / Store apps without direct `.lnk`).
   - Direct PE resource modification of `.exe` / `.dll` files.

### 3.3 Mathematical Styling Contract
1. **Normalization**:
   - Bounding box calculation ignores alpha values $< 16$ (`threshold = 16`).
   - Aspect ratio is strictly preserved with uniform scaling into the inner area defined by `1.0 - 2 * Padding`.
   - Centering is exact: $\lfloor(W - w)/2\rfloor$, $\lfloor(H - h)/2\rfloor$.
2. **Visual Treatment**:
   - `Saturation` $[-1.0, +1.0]$: Rec.709 grayscale luminance weights ($Y = 0.2126R + 0.7152G + 0.0722B$).
   - `Brightness` $[-1.0, +1.0]$: Linear offset applied to normalized RGB channels.
   - `Contrast` $[-1.0, +1.0]$: Scaled around pivot $0.5$.
3. **Container**:
   - `Squircle`: Superellipse $|(x - c_x)/r|^{4.5} + |(y - c_y)/r|^{4.5} \le 1.0$ with $4\times 4$ supersampling anti-aliasing.
   - `RoundedSquare`: Box with corner radius $= 22.5\%$ of container dimension.
   - `Circle`: Centered inscribed disk with radius $= 0.5 \times D$.
4. **Resolution Output**:
   - Master canvas rendered at $256\times 256$ RGBA.
   - Converted to multi-resolution 7-layer `.ico` ($16, 24, 32, 48, 64, 128, 256$) via `IcoEncoder`.

### 3.4 Storage & Cache Isolation
- `%LOCALAPPDATA%\WindowsIconsAdmin\Styles\`: User style definitions (`.json`).
- `%LOCALAPPDATA%\WindowsIconsAdmin\GeneratedIcons\{cacheKey}.ico`: Deterministic rendered icons.
- `%LOCALAPPDATA%\WindowsIconsAdmin\OriginalSnapshots\{appId}.png`: Original extracted icons.
- `%LOCALAPPDATA%\WindowsIconsAdmin\State\app-undo.json`: Dedicated app undo transaction store.

---

## 4. Consequences

### Positive
- **Guaranteed System Safety**: User application binaries are never touched; only shortcut metadata is updated.
- **Deterministic Quality**: Bitwise reproducible renders across identical styles and source icons.
- **Independent Undo**: Undoing folder changes does not collide with or revert application icon changes.
- **Cross-Platform Testability**: The math, normalizer, and styling engine run in pure memory and are testable without Windows OS dependencies.

### Negative / Trade-offs
- Modifying a `.lnk` does not alter running window titles or Alt+Tab icons of processes already loaded into memory.
- Store apps (MSIX) that do not generate desktop or start menu `.lnk` shortcuts cannot be styled in the first MVP.
