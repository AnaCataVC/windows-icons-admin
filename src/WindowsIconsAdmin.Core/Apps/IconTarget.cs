namespace WindowsIconsAdmin.Core.Apps;

public sealed record IconTarget(
    IconTargetType TargetType,
    string TargetPath,
    string? CurrentIconLocation,
    int CurrentIconIndex,
    AppSupportStatus SupportStatus,
    string? UnsupportedReason = null);
