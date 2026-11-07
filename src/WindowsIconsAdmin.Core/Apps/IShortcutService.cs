namespace WindowsIconsAdmin.Core.Apps;

public interface IShortcutService
{
    bool ShortcutExists(string shortcutPath);
    (string TargetPath, string? IconLocation, int IconIndex) ReadShortcut(string shortcutPath);
    void SetShortcutIcon(string shortcutPath, string iconLocation, int iconIndex);
}
