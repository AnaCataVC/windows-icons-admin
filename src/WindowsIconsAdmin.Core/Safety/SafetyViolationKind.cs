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
