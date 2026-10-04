# ADR 0003: Native COM Multi-Folder Selection and Persistent Rules Engine

- **Status:** Accepted
- **Date:** 2026-10-04
- **Deciders:** Core Engineering Team
- **Consulted:** Cleanroom Rule Engine Specification, UX & Shell Stability Guidelines

---

## 1. Context and Problem Statement

User feedback and usability testing highlighted three limitations in the initial folder selection and rule-based automation workflows:
1. **Single-Folder Selection Bottleneck:** WinUI 3's `Windows.Storage.Pickers.FolderPicker` only supports picking one directory per invocation (`PickSingleFolderAsync`), making it tedious to assign the same icon to multiple folders at once.
2. **Volatile and Unintuitive Rules:** Rules were stored only in memory (`List<FolderRule>`), disappearing when the application closed. Furthermore, users lacked wildcard matching (`*`, `?`), full-path matching, priority reordering, live match previews, and auto-saving when clicking the primary apply action directly from the editor form.
3. **Silent Rule Execution Failures & Re-Application Errors:** Errors during rule icon encoding or missing icon files were silently swallowed, and re-applying an icon in `PortableEmbedded` mode could fail if `.folder_icon.ico` already existed with `FileAttributes.Hidden`.

---

## 2. Alternatives Considered

| Dimension | Alternative A: WinRT Picker Only + In-Memory Rules | Alternative B: Custom TreeView Browser Dialog | Alternative C: Win32 COM `IFileOpenDialog` + Subfolder Loader + Persistent `RuleStore` (Selected) |
| :--- | :--- | :--- | :--- |
| **Multi-Folder Selection** | Requires N separate dialog invocations | Loses native Explorer Quick Access, network shares, and search bar | **Native Windows Explorer multi-selection (`Ctrl`/`Shift` + click) plus bulk subfolder loader** |
| **Rule Expressiveness** | Basic substring / raw Regex only | Basic substring only | **Contains, StartsWith, EndsWith, Equals, Wildcard (`*`, `?`), and timeout-guarded Regex across Name or FullPath** |
| **Rule Persistence** | Lost on app exit | Ad-hoc settings keys | **Atomic JSON persistence (`RuleStore`) with temp-file swap and `.bak` corruption recovery** |
| **User Feedback** | Post-hoc batch count only | Post-hoc batch count only | **Live per-rule match counter, interactive sample tester, and auto-save on apply** |

---

## 3. Decision

### 3.1 Multi-Folder Selection (`MultiFolderPickerService`)
- **Native COM Multi-Select:** Implemented `MultiFolderPickerService` invoking `CLSID_FileOpenDialog` (`IFileOpenDialog`) with `FOS_PICKFOLDERS | FOS_ALLOWMULTISELECT | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST`, returning all selected directories from `IShellItemArray` with automatic fallback to `Windows.Storage.Pickers.FolderPicker`.
- **Direct Subfolder Loader:** Added an *"Add subfolders of..."* workflow allowing users to pick a parent root directory and load all valid first-level subdirectories in a single action (filtering out hidden dot-folders and protected system folders via `SystemFolderGuard`).
- **Extended List Selection:** Enabled `SelectionMode="Extended"` on the main folder list alongside per-row checkboxes, live checked counters, single-item removal, and a *"Check highlighted only"* action.

### 3.2 Persistent & Expressive Rules Engine (`RuleEngine` & `RuleStore`)
- **Expanded Conditions & Targets:** Extended `RuleCondition` with `Equals` and `Wildcard` (compiled to anchored regular expressions with a 1-second `RegexTimeout` to preserve ReDoS immunity) and added `RuleMatchTarget` (`FolderName` vs `FullPath`) with backward-compatible default parameters on `FolderRule`.
- **Atomic Persistence (`RuleStore`):** Persists rules to `%LOCALAPPDATA%\WindowsIconsAdmin\rules.json` using atomic `File.Replace` temp-file swaps and `.bak` recovery on malformed JSON, mirroring `UndoStore` reliability invariants.
- **Master-Detail Rules Dialog:** Redesigned `RulesDialog` into a two-column Master-Detail interface featuring enable/disable toggles, priority reordering (`▲`/`▼`), rule duplication, icon thumbnail preview, live folder match counts, an interactive sample folder name tester, and automatic saving of pending form inputs when clicking *"Apply rules now"*.

---

## 4. Consequences

### Positive
- **Frictionless Bulk Selection:** Users can select dozens of folders in seconds via native `Ctrl`/`Shift` selection, drag-and-drop, or parent folder expansion.
- **Predictable Rule Authoring:** Live match previews and the built-in pattern tester eliminate guesswork before modifying folders on disk.
- **Zero Data Loss:** Rules persist reliably across sessions, and uncommitted form edits are automatically validated and saved when applying rules.
