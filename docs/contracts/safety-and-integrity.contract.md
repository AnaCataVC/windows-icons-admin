# Contract: Safety & Windows Integrity v1 (SystemFolderGuard, Hardened IniHelper, Safe Revert & KnownFolders)

Status: FROZEN for cleanroom cycle 2. Language: C# 13 / .NET 9, `Nullable` enabled.  
Target Assemblies: `WindowsIconsAdmin.Core`.  
Test Framework: xUnit (`tests/WindowsIconsAdmin.Core.Tests`).  

This contract is the only shared source of truth between implementer (`ami-cleanroom-builder`) and black-box tester (`ami-cleanroom-tester`). Anything not stated here is unspecified and MUST NOT be asserted by tests nor relied upon.

---

## 1. Domain Types & Signatures

### 1.1 Violation Enumeration and Result Type (`WindowsIconsAdmin.Core.Safety`)

```csharp
namespace WindowsIconsAdmin.Core.Safety;

public enum SafetyViolationKind
{
    None = 0,
    DriveRoot,              // e.g. "C:\", "D:\", "/"
    SystemRoot,             // e.g. %SystemRoot%, "C:\Windows", System32
    ProgramFiles,           // e.g. %ProgramFiles%, %ProgramFiles(x86)%
    UserProfileRoot,        // e.g. %USERPROFILE% ("C:\Users\username")
    AppDataSensitive,       // e.g. %LOCALAPPDATA%, %APPDATA%
    PathTraversal,          // contains "..", directory separators where forbidden, escapes target
    RestrictedFileExtension,// .exe, .dll, .sys, .bat, .cmd, .lnk, .com, .scr, .msi, .vbs, .ps1, .reg, .cpl
    InvalidCharacters       // control chars, newlines, invalid filesystem chars
}

public sealed record SafetyValidationResult(
    bool IsValid,
    SafetyViolationKind ViolationKind,
    string? Reason)
{
    public static SafetyValidationResult Success() =>
        new(true, SafetyViolationKind.None, null);

    public static SafetyValidationResult Violation(SafetyViolationKind kind, string reason) =>
        new(false, kind, reason);
}
```

### 1.2 Custom Domain Exceptions (`WindowsIconsAdmin.Core.Safety`)

```csharp
namespace WindowsIconsAdmin.Core.Safety;

public class SystemProtectionException : Exception
{
    public SafetyViolationKind ViolationKind { get; }
    public SystemProtectionException(string message, SafetyViolationKind kind = SafetyViolationKind.SystemRoot)
        : base(message)
    {
        ViolationKind = kind;
    }
}

public class PathTraversalException : Exception
{
    public PathTraversalException(string message) : base(message) { }
}

public class RestrictedExtensionException : Exception
{
    public string Extension { get; }
    public RestrictedExtensionException(string message, string extension)
        : base(message)
    {
        Extension = extension;
    }
}
```

### 1.3 System Folder Guard (`WindowsIconsAdmin.Core.Safety`)

```csharp
namespace WindowsIconsAdmin.Core.Safety;

public static class SystemFolderGuard
{
    public static readonly IReadOnlySet<string> RestrictedExtensions;

    // Dual inspection & assertion API
    public static SafetyValidationResult ValidateTargetFolder(string? folderPath);
    public static void EnsureSafeTargetFolder(string? folderPath); // throws ArgumentException or SystemProtectionException

    public static SafetyValidationResult ValidateEmbeddedFileName(string? fileName);
    public static void EnsureSafeEmbeddedFileName(string? fileName); // throws ArgumentException or PathTraversalException

    public static SafetyValidationResult ValidateExtension(string? extension);
    public static void EnsureSafeExtension(string? extension); // throws ArgumentException or RestrictedExtensionException

    public static bool IsProtectedFolder(string folderPath);
    public static bool IsKnownFolder(string folderPath);
    public static bool IsRestrictedExtension(string extension);

    // Path containment validator
    public static bool IsContainedWithin(string parentPath, string candidateChildPath);
}
```

### 1.4 Hardened IniHelper (`WindowsIconsAdmin.Core.Shell`)

```csharp
namespace WindowsIconsAdmin.Core.Shell;

public static class IniHelper
{
    public static string SetIconResource(string? existingContent, string iconResource);

    public static string? RemoveShellClassInfo(
        string? existingContent,
        bool preserveShellDirectives = false);
}
```

### 1.5 Safe Rollback and Known Folder Restoration Contract (`WindowsIconsAdmin.Core.Shell`)

```csharp
// Extensions / Behaviors in ShellIconService
namespace WindowsIconsAdmin.Core.Shell;

// Public behavior guarantees on ShellIconService:
// 1. RevertFolder(FolderSnapshot snapshot):
//    - Validates snapshot.CopiedIconFileName with SystemFolderGuard.ValidateEmbeddedFileName.
//    - Uses IsContainedWithin to verify that target file is strictly inside snapshot.FolderPath.
//    - If CopiedIconFileName violates safety, RevertFolder skips file deletion and does not throw (avoids crashing batch undo).
//
// 2. RestoreFolderDefault(string folderPath, bool isKnownFolder = false):
//    - If isKnownFolder == true or SystemFolderGuard.IsKnownFolder(folderPath) == true:
//      - Keeps FILE_ATTRIBUTE_READONLY / FILE_ATTRIBUTE_SYSTEM on folderPath.
//      - Invokes IniHelper.RemoveShellClassInfo with preserveShellDirectives: true so LocalizedResourceName and CLSID are NOT destroyed.
```

---

## 2. Behavioral Acceptance Criteria

### 2.1 Target Folder Safety (`ValidateTargetFolder` & `EnsureSafeTargetFolder`)
- **SC-F1 (Null or Empty):** Given `null`, `""` or whitespace, returns `Violation(InvalidCharacters, ...)` and `EnsureSafeTargetFolder` throws `ArgumentException`.
- **SC-F2 (Drive Root):** Given `"C:\"`, `"C:"`, `"D:\"`, `"\\"` or `"/"`, returns `Violation(DriveRoot, ...)` and throws `SystemProtectionException` with `ViolationKind == DriveRoot`.
- **SC-F3 (Windows & System Roots):** Given any path equal to or inside `%SystemRoot%` (e.g. `"C:\Windows"`, `"C:\Windows\System32"`, `"C:\Windows\SysWOW64"`), returns `Violation(SystemRoot, ...)`.
- **SC-F4 (Program Files):** Given any path equal to or inside `%ProgramFiles%` or `%ProgramFiles(x86)%` (e.g. `"C:\Program Files"`, `"C:\Program Files (x86)\Common Files"`), returns `Violation(ProgramFiles, ...)`.
- **SC-F5 (User Profile Root):** Given a path exactly equal to `%USERPROFILE%` (e.g. `"C:\Users\username"`), returns `Violation(UserProfileRoot, ...)`. Child user directories (e.g. `"C:\Users\username\Projects"`) are valid, UNLESS they match `%LOCALAPPDATA%` or `%APPDATA%`.
- **SC-F6 (Sensitive AppData):** Given any path equal to or inside `%APPDATA%` or `%LOCALAPPDATA%` (except the application's own cache folder `"WindowsIconsAdmin"`), returns `Violation(AppDataSensitive, ...)`.
- **SC-F7 (Safe User Folders):** Given a regular user directory (e.g. `"C:\Users\username\Desktop\MyFolder"`, `"D:\Projects\Code"`), returns `Success()` and does not throw.

### 2.2 Embedded File Name Anti-Traversal (`ValidateEmbeddedFileName` & `EnsureSafeEmbeddedFileName`)
- **SC-E1 (Null or Empty):** Given `null` or whitespace, returns `Violation(InvalidCharacters, ...)` and throws `ArgumentException`.
- **SC-E2 (Traversal Sequences):** Given `"../icon.ico"`, `"..\\icon.ico"`, `"folder/icon.ico"`, `"C:\\icon.ico"`, or anything where `Path.GetFileName(fileName) != fileName`, returns `Violation(PathTraversal, ...)` and throws `PathTraversalException`.
- **SC-E3 (Invalid Characters):** Given any string containing invalid file characters (`Path.GetInvalidFileNameChars()`), returns `Violation(InvalidCharacters, ...)`.
- **SC-E4 (Extension Restriction):** Must strictly end with `".ico"` (case-insensitive). Given `".folder_icon.ico"` returns `Success()`. Given `"icon.exe"`, `"script.bat"`, `"icon.png"`, returns `Violation(RestrictedFileExtension, ...)` or `Violation(InvalidCharacters, ...)`.

### 2.3 File Extension Safety (`ValidateExtension` & `EnsureSafeExtension`)
- **SC-X1 (Restricted List):** The restricted list MUST include at minimum:
  `.exe`, `.dll`, `.sys`, `.bat`, `.cmd`, `.com`, `.scr`, `.msi`, `.vbs`, `.ps1`, `.reg`, `.lnk`, `.cpl`, `.drv`.
- **SC-X2 (Denylist Match):** Given any extension in the restricted list (with or without leading `.` and in any casing, e.g. `"exe"`, `".EXE"`, `".bat"`), returns `Violation(RestrictedFileExtension, ...)` and throws `RestrictedExtensionException`.
- **SC-X3 (Allowed Extensions):** Given standard data/document extensions (e.g. `".txt"`, `".pdf"`, `".docx"`, `".json"`, `".py"`, `".custom"`), returns `Success()`.

### 2.4 Path Containment (`IsContainedWithin`)
- **SC-C1 (Inside Child):** Given `parent = @"C:\Folder"` and `child = @"C:\Folder\sub\file.ico"`, returns `true`.
- **SC-C2 (Escaping Traversal):** Given `parent = @"C:\Folder"` and `child = @"C:\Folder\..\Other\file.ico"`, returns `false`.
- **SC-C3 (Exact Match):** Given `parent` and `child` pointing to the exact same path, returns `false` (child must be within parent).
- **SC-C4 (Prefix Collision Safety):** Given `parent = @"C:\Folder"` and `child = @"C:\FolderNotChild\file.ico"`, returns `false` (must not match string prefix without directory separator boundary).

### 2.5 Hardened `IniHelper.SetIconResource` (Anti-Injection)
- **SC-I1 (Newlines Forbidden):** If `iconResource` contains `\r` or `\n`, throws `ArgumentException` with message stating newlines are not permitted.
- **SC-I2 (Brackets Forbidden):** If `iconResource` contains `[` or `]`, throws `ArgumentException`.
- **SC-I3 (Valid Injection Preserved):** Given valid resource `"C:\Icons\myicon.ico,0"`, updates or creates `[.ShellClassInfo]` with `IconResource=C:\Icons\myicon.ico,0` while preserving comments, empty lines, and existing sections like `[ViewState]`.

### 2.6 Hardened `IniHelper.RemoveShellClassInfo` (Preservation of KnownFolder Directives)
- **SC-K1 (Standard Folder - preserveShellDirectives = false):** Completely removes `[.ShellClassInfo]`. If no other sections remain, returns `null`.
- **SC-K2 (KnownFolder with System Directives - preserveShellDirectives = true):**
  Given existing content:
  ```ini
  [.ShellClassInfo]
  LocalizedResourceName=@%SystemRoot%\system32\shell32.dll,-21770
  IconResource=C:\Custom\icon.ico,0
  CLSID={FDD39AD0-238F-46AF-ADB4-6C85480369C7}
  ```
  When `RemoveShellClassInfo(content, preserveShellDirectives: true)` is called, it removes `IconResource=...` (and `IconFile=`, `IconIndex=` if present), but **RETAINS** `[.ShellClassInfo]`, `LocalizedResourceName=...` and `CLSID=...`. The returned string is NOT `null`.
- **SC-K3 (KnownFolder with Only Icon in ShellClassInfo):**
  If `[.ShellClassInfo]` contained only `IconResource=...` and no other keys, `RemoveShellClassInfo(content, preserveShellDirectives: true)` removes `[.ShellClassInfo]`. If no other sections exist, returns `null`.

### 2.7 Safe Rollback (`RevertFolder`)
- **SC-R1 (Safe Copied Icon Delete):** If `snapshot.CopiedIconFileName` is valid (e.g. `".folder_icon.ico"`) and file exists inside `snapshot.FolderPath`, it is deleted.
- **SC-R2 (Unsafe Copied Icon Skipped):** If `snapshot.CopiedIconFileName` contains traversal (e.g. `"../../important.doc"` or `"evil.exe"`), it is NOT deleted, avoiding arbitrary deletion.

---

## 3. Boundary Values & Edge Cases
1. **Trailing Slashes:** Paths with or without trailing backslashes/slashes (e.g. `"C:\Windows\"` vs `"C:\Windows"`) must be normalized and treated identically.
2. **Case Insensitivity:** Windows paths and extensions are case-insensitive (`"c:\windows"` == `"C:\WINDOWS"`).
3. **Environment Variable Expansion:** Paths referencing `%SystemRoot%`, `%ProgramFiles%`, `%USERPROFILE%`, etc. must be resolved to their canonical full paths using `Path.GetFullPath`.
4. **Non-ASCII & Unicode Characters:** Folder paths and file names containing accents, spaces, or international scripts must be properly supported without corruption.

---

## 4. Prohibited Details
- Internal collections representation (e.g. whether `HashSet` or `FrozenSet` is used for `RestrictedExtensions`).
- Exact phrasing of exception messages (tests must assert exception types and properties like `ViolationKind` or `Extension`, not string exact match).
- Private helper method names or file structure beyond the declared public types.
