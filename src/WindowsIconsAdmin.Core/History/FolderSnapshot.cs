namespace WindowsIconsAdmin.Core.History;

public sealed record FolderSnapshot(
    string FolderPath,
    bool HadDesktopIni,
    string? PreviousDesktopIniContent,
    uint PreviousFolderAttributes,
    uint? PreviousDesktopIniAttributes,
    string? CopiedIconFileName);
