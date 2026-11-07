namespace WindowsIconsAdmin.Core.Apps;

public sealed record InstalledApp(
    string Id,
    string DisplayName,
    string? ExecutablePath,
    string? OriginalIconSource,
    IReadOnlyList<IconTarget> Targets)
{
    public AppSupportStatus SupportStatus => Targets switch
    {
        { Count: 0 } => AppSupportStatus.Unsupported,
        _ when Targets.Any(t => t.SupportStatus == AppSupportStatus.Supported) => AppSupportStatus.Supported,
        _ when Targets.Any(t => t.SupportStatus == AppSupportStatus.PartiallySupported) => AppSupportStatus.PartiallySupported,
        _ => AppSupportStatus.Unsupported
    };
}
