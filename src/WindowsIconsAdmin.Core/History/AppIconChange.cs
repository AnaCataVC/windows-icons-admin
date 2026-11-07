using WindowsIconsAdmin.Core.Apps;

namespace WindowsIconsAdmin.Core.History;

public sealed record AppIconChange(
    string AppId,
    string AppDisplayName,
    string TargetPath,
    IconTargetType TargetType,
    string? PreviousIconLocation,
    int PreviousIconIndex,
    string? OriginalSnapshotPath,
    string AppliedIconPath,
    DateTimeOffset TimestampUtc);
