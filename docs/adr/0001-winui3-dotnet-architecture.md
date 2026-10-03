# ADR 0001: Architecture Selection: .NET 9 WinUI 3, Isolated Core Library, and Cleanroom TDD

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** ami-doc-architect, Core Engineering Team
- **Consulted:** Windows Shell Automation Research, Core Services Contract v1

---

## 1. Context and Problem Statement

Customizing folder icons in Windows has historically been divided between cumbersome manual property dialogs (one folder at a time, requiring pre-converted `.ico` files) and dated legacy utilities. To build a modern, high-performance Windows folder icon management utility, we need an architecture that delivers:
1. Native Windows 11 Fluent design with dark mode and high-DPI scaling.
2. Low-overhead, zero-lag batch processing for hundreds of directories.
3. Clean separation between domain logic (rules, image encoding, undo transaction history) and platform shell integration.
4. Deterministic quality through strict Test-Driven Development (TDD).

---

## 2. Alternatives Considered

We evaluated three architectural candidates across key dimensions:

| Dimension | Option A: Python 3.11 + PySide6 | Option B: Tauri (Rust + WebView2) | Option C: C# .NET 9 + WinUI 3 (Selected) |
| :--- | :--- | :--- | :--- |
| **UI Modernity** | Qt Quick / QML (looks non-native or approximated on Win11) | Web tech (HTML/CSS/JS) via WebView2 | **Native Windows App SDK (WinUI 3)** Fluent controls, Mica/Acrylic material, native dark mode |
| **Win32 API Access** | `ctypes` / `pywin32` (error-prone pointer handling, runtime crashes) | Rust `windows` crate (fast, typed, high safety) | **Native P/Invoke (`CsWin32` / `DllImport`)** direct integration with Kernel32 & Shell32 |
| **Explorer Context Menu** | Registry verbs only; native COM DLL requires separate C++ toolchain | Registry verbs; native C++ COM DLL required for top-level menu | Direct C++ bridge or sparse package identity; seamless COM interop |
| **Distribution Size** | 45–65 MB bundled via PyInstaller | 5–12 MB lean binary | Self-contained or framework-dependent Windows App SDK bundle |
| **Startup Latency** | High (Python interpreter startup overhead) | Low (native Rust entry point) | **Near-instantaneous** (AOT or compiled .NET 9 JIT) |
| **Domain Logic Purity** | Difficult to isolate from C extension bindings | Strongly isolated in Rust crates | **Isolated `WindowsIconsAdmin.Core` class library**, 100% testable without OS dependencies |

### Summary of Alternatives Rejected
- **Python + PySide6:** While excellent for rapid exploratory prototyping, bundling Python for end-user Windows software incurs high binary overhead, slow cold startup, and complex distribution signing.
- **Tauri (Rust + WebView2):** While lean, WebView2 introduces web-to-native IPC latency for dense icon grid previews and lacks native Windows Shell visual consistency out-of-the-box.

---

## 3. Decision

We chose **C# 13 and .NET 9 with WinUI 3 (Windows App SDK)**, organized under a **Clean Architecture** model with strict **Cleanroom TDD**.

```
+-------------------------------------------------------------+
|                 WindowsIconsAdmin.App (WinUI 3)              |
|        - Modern Fluent UI (Mica, Navigation, Drag & Drop)   |
|        - Explorer Context Menu Single-Instance IPC Bridge   |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|              WindowsIconsAdmin.ShellServices                |
|        - P/Invoke: SetFileAttributesW, SHChangeNotify       |
|        - desktop.ini Generator & Parser                     |
|        - Shell Association & Context Menu Registry Verbs    |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|                WindowsIconsAdmin.Core (.NET 9)               |
|        - Rules Engine (RuleEngine, RuleCondition, Matching) |
|        - Imaging (IcoEncoder: Multi-res DIB + PNG256)       |
|        - History & Undo Store (Transactional JSON logging)  |
|        - Completely pure, 0 external filesystem coupling    |
+-------------------------------------------------------------+
```

### 3.1 Isolated Core Class Library (`WindowsIconsAdmin.Core`)
All core domain logic is isolated in a target-independent class library:
- **`WindowsIconsAdmin.Core.Rules`**: Evaluates folder names against user-defined rules (`Contains`, `StartsWith`, `EndsWith`, `Regex`) with priority ordering and regex catastrophic backtracking timeouts.
- **`WindowsIconsAdmin.Core.Imaging`**: Generates valid 7-layer multi-resolution `.ico` binaries from arbitrary PNG inputs. Includes manual PNG chunk validation to defend against silent GDI+ truncation flaws.
- **`WindowsIconsAdmin.Core.History`**: Transactional, thread-safe undo logging (`UndoStore`) with atomic file replacement (`.tmp` write followed by `File.Replace`), rolling batch retention (max 50 batches), and automatic corrupt file recovery (`.bak`).

### 3.2 Icon Storage Strategy: Hybrid Approach
We evaluated two competing icon persistence strategies:
1. **Portable Embedded Copy (`IconResource=.\.folder_icon.ico,0`):**
   - *Pros:* Moving, renaming, or transferring the folder across drives preserves the icon.
   - *Cons:* Adds a hidden file inside the user's directory.
2. **Central Application Cache (`%LOCALAPPDATA%\WindowsIconsAdmin\Icons\...`):**
   - *Pros:* User folders remain untouched; no extra files inside folder roots.
   - *Cons:* Moving the folder to another computer breaks the icon link.

**Strategy Decision:** WindowsIconsAdmin implements a **hybrid model**:
- **Default for User Folders:** Portable embedded copy (hidden + system attributes).
- **Default for System / CLSID Targets (Recycle Bin, Drives, This PC):** Central application cache in `%LOCALAPPDATA%`, as registry-based system icons require stable, permanent absolute paths.

---

## 4. Methodology: Cleanroom TDD Workflow

To eliminate developer bias and ensure verifiable correctness, we enforced a **Cleanroom Software Engineering** process:

1. **Specification as Frozen Contract:**
   The interface, constraints, edge cases, and acceptance scenarios were formalized into `docs/contracts/core-services.contract.md` before implementation.
2. **Air-Gapped Test & Implementation:**
   The test suite was written strictly against the frozen specification without knowledge of internal implementation details. Implementers had no access to test source code during initial authoring.
3. **Zero Test Pollution:**
   All 160 unit tests run in-memory or against isolated temporary directory scopes.
4. **Current Verification Metrics:**
   - **Total Tests:** 160 passing (100% pass rate).
   - **Test Framework:** xUnit on .NET 9.
   - **Execution Time:** ~2.0 seconds.

---

## 5. Consequences & Trade-offs

### Positive
- **Guaranteed Robustness:** High test coverage and strict boundary checks eliminate regressions before UI binding.
- **Performance:** Direct binary DIB/PNG assembly in memory without disk intermediate files.
- **Maintainability:** Pure core library can be reused in CLI tools, background daemons, or PowerShell modules.
- **Modern UX:** WinUI 3 delivers native Windows 11 visuals without third-party skinning libraries.

### Negative / Trade-offs
- **Windows-Only:** Tying the presentation layer to WinUI 3 restricts the GUI to Windows 10/11. (This aligns with the product's sole purpose as a Windows Shell administrator).
- **Windows 11 Context Menu Complexity:** Modern top-level Explorer menu integration requires a native C++ `IExplorerCommand` COM bridge and package identity, requiring a staged release (classic registry verbs first, sparse package bridge second).
