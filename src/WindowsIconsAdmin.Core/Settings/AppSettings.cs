namespace WindowsIconsAdmin.Core.Settings;

public sealed record AppSettings
{
    /// <summary>
    /// When true, allows the user to select PortableEmbedded mode (.folder_icon.ico inside folders).
    /// Default is false, which enforces CentralCache in %LOCALAPPDATA% to keep folders 100% clean.
    /// </summary>
    public bool EnablePortableMode { get; init; } = false;
}
