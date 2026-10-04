# ADR 0002: System Folder Guard, desktop.ini Hardening, and KnownFolders Preservation

- **Status:** Accepted
- **Date:** 2026-10-04
- **Deciders:** Core Engineering Team, Repository Security Auditor
- **Consulted:** Safety & Integrity Contract v1, Cleanroom Cycle 2 Implementation

---

## 1. Context and Problem Statement

A security and system integrity audit revealed several critical vectors in automated Windows folder icon administration:
1. **Unconstrained Target Folders:** Bulk rule evaluation could inadvertently match root filesystem drives (`C:\`), Windows system roots (`C:\Windows`, `System32`), User profiles root (`C:\Users\<user>`), or application installation directories (`Program Files`). Customizing these locations disrupts Windows Update, file indexing, and ACL inheritance.
2. **INI Injection in `desktop.ini`:** Custom icon resource paths passed to `WritePrivateProfileStringW` or raw file streams without character filtering could contain carriage return/line feed (`\r\n`) sequences or square bracket markers (`[` / `]`), enabling arbitrary section spoofing (such as injecting shell extension CLSIDs like `[{F0262F40-87E4-11D1-885C-0000F87579AC}]`).
3. **Destruction of Windows `KnownFolders`:** Resetting or reverting folder customizations by deleting `desktop.ini` and stripping `FILE_ATTRIBUTE_READONLY` stripped critical shell directives in special system folders (Downloads, Documents, Pictures, Music, Desktop). This resulted in Windows losing localized folder display names (`LocalizedResourceName`) and namespace behavior.
4. **Shell Association Hijacking:** Modifying `DefaultIcon` for core executable types (`.exe`, `.bat`, `.cmd`, `.dll`) breaks default executable icon extraction (`%1`) and creates binary spoofing risks.

---

## 2. Alternatives Considered

| Dimension | Alternative A: Ad-Hoc UI Warnings | Alternative B: Shell Hook / ACL Locks | Alternative C: Domain Protection Layer (Selected) |
| :--- | :--- | :--- | :--- |
| **Enforcement Point** | WinUI 3 presentation layer only | Windows NTFS ACLs or file system filter | **Pure Domain Core (`SystemFolderGuard` & `IniHelper`)** |
| **Bypass Vulnerability** | High (CLI, tests, or background engines bypass UI) | High (modifying ACLs requires elevated UAC, introduces permanent lockouts) | **Zero (centralized domain guard called by all services)** |
| **KnownFolders Safety** | Fragile (user must manually avoid special folders) | N/A | **Granular attribute preservation & selective INI pruning** |
| **Testability** | Requires end-to-end UI tests | Requires elevated OS drivers or sandbox | **100% unit-testable via mockable contracts and air-gapped suites** |

---

## 3. Decision

We implemented a defense-in-depth architecture consisting of three coordinated subsystems:

### 3.1 `SystemFolderGuard` Domain Service
- **Dual Pattern:** Provides both non-throwing `ValidateTargetFolder` returning `SafetyValidationResult` (for UI display and batch filtering) and throwing `EnsureSafeFolder` (for internal execution boundaries).
- **Canonical Path Normalization:** Resolves relative segments (`..`), strips trailing directory separators, and performs case-insensitive comparisons against:
  - System root directories (`C:\Windows`, `System32`, `SysWOW64`, `Program Files`, `ProgramData`).
  - Volume roots (`C:\`, `D:\`).
  - User profiles root (`C:\Users\<user>`) and sensitive application data (`AppData\Local`, `AppData\Roaming`).
- **Extension Denylist:** `EnsureSafeExtension` enforces strict rejection of executable, script, and system binary extensions (`.exe`, `.bat`, `.cmd`, `.dll`, `.com`, `.scr`, `.vbs`, `.ps1`, `.msi`, `.sys`).

### 3.2 Hardened `IniHelper`
- **Injection Sanitization:** Rejects or strips `\r`, `\n`, `[`, and `]` in icon resource paths and icon file parameters.
- **Section Preservation:** When updating or clearing icons in existing `desktop.ini` files, `preserveShellDirectives` ensures native sections (such as `[ViewState]`, `LocalizedResourceName`, and custom CLSIDs) remain intact.

### 3.3 Safe Rollback in `ShellIconService`
- **Attribute Retention:** When reverting or clearing folder icons, `KnownFolders` retain their `FILE_ATTRIBUTE_READONLY` / `FILE_ATTRIBUTE_SYSTEM` attributes so Windows Explorer continues resolving localized directory names.
- **Path Traversal Defense:** Reverting local copied icons checks `CopiedIconFileName` via strict filename extraction (`Path.GetFileName`), preventing directory traversal deletion attacks.

---

## 4. Consequences

### Positive
- **Guaranteed System Stability:** Core Windows folders and volume roots are impervious to accidental modification.
- **Shell Integrity:** Windows Explorer localized names (e.g., "Descargas" instead of "Downloads") remain intact across all icon customization cycles.
- **Safe Rollback:** Transactional undo operations cannot delete arbitrary files outside the designated icon cache.
- **Comprehensive Test Coverage:** 157 new unit tests added (totaling 345 passing tests) confirming all boundary conditions.

### Negative / Trade-offs
- Customizing the root of an external drive or a user profile folder is intentionally blocked; users must customize subdirectories instead.
