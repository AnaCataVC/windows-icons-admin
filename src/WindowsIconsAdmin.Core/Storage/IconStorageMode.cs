namespace WindowsIconsAdmin.Core.Storage;

public enum IconStorageMode
{
    /// <summary>
    /// Copies the icon as a hidden system file inside the folder (.folder_icon.ico) with a relative path.
    /// Portable across drives, USBs and renames.
    /// </summary>
    PortableEmbedded,

    /// <summary>
    /// Stores the icon centrally in %LOCALAPPDATA%\WindowsIconsAdmin\Icons\ and uses an absolute path.
    /// Leaves the target folder clean (no extra files).
    /// </summary>
    CentralCache
}
