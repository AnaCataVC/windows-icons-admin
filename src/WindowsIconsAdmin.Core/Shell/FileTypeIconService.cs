using Microsoft.Win32;

namespace WindowsIconsAdmin.Core.Shell;

public class FileTypeIconService
{
    public static void SetExtensionIcon(string extension, string iconPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);

        extension = NormalizeExtension(extension);
        ShellIconService.ValidateLocalIconPath(iconPath);
        var formatted = ShellIconService.FormatIconResourcePath(iconPath);

        // 1. Set under HKCU\Software\Classes\SystemFileAssociations\.ext\DefaultIcon
        var sysAssocKey = $@"Software\Classes\SystemFileAssociations\{extension}\DefaultIcon";
        using (var key = Registry.CurrentUser.CreateSubKey(sysAssocKey, writable: true))
        {
            key.SetValue("", formatted, RegistryValueKind.String);
        }

        // 2. Also associate to current user ProgId if present
        var progId = GetExtensionProgId(extension);
        if (!string.IsNullOrEmpty(progId))
        {
            var progIdKey = $@"Software\Classes\{progId}\DefaultIcon";
            using var pKey = Registry.CurrentUser.CreateSubKey(progIdKey, writable: true);
            pKey.SetValue("", formatted, RegistryValueKind.String);
        }

        // 3. Fallback direct HKCU extension class DefaultIcon
        var extDirectKey = $@"Software\Classes\{extension}\DefaultIcon";
        using (var eKey = Registry.CurrentUser.CreateSubKey(extDirectKey, writable: true))
        {
            eKey.SetValue("", formatted, RegistryValueKind.String);
        }

        ShellIconService.NotifyAssociationChanged();
    }

    public static void RestoreExtensionIcon(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        extension = NormalizeExtension(extension);

        // Remove from SystemFileAssociations
        var sysAssocKey = $@"Software\Classes\SystemFileAssociations\{extension}";
        using (var key = Registry.CurrentUser.OpenSubKey(sysAssocKey, writable: true))
        {
            key?.DeleteSubKeyTree("DefaultIcon", false);
        }

        // Remove from direct extension class
        var extDirectKey = $@"Software\Classes\{extension}";
        using (var eKey = Registry.CurrentUser.OpenSubKey(extDirectKey, writable: true))
        {
            eKey?.DeleteSubKeyTree("DefaultIcon", false);
        }

        // Remove from ProgID if created under HKCU
        var progId = GetExtensionProgId(extension);
        if (!string.IsNullOrEmpty(progId))
        {
            var progIdKey = $@"Software\Classes\{progId}";
            using var pKey = Registry.CurrentUser.OpenSubKey(progIdKey, writable: true);
            pKey?.DeleteSubKeyTree("DefaultIcon", false);
        }

        ShellIconService.NotifyAssociationChanged();
    }

    public static string? GetExtensionIcon(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        extension = NormalizeExtension(extension);

        // Check SystemFileAssociations
        var sysAssocKey = $@"Software\Classes\SystemFileAssociations\{extension}\DefaultIcon";
        using (var key = Registry.CurrentUser.OpenSubKey(sysAssocKey))
        {
            var val = key?.GetValue("") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        // Check direct extension
        var extDirectKey = $@"Software\Classes\{extension}\DefaultIcon";
        using (var eKey = Registry.CurrentUser.OpenSubKey(extDirectKey))
        {
            var val = eKey?.GetValue("") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        // Check ProgID
        var progId = GetExtensionProgId(extension);
        if (!string.IsNullOrEmpty(progId))
        {
            var progIdKey = $@"Software\Classes\{progId}\DefaultIcon";
            using var pKey = Registry.CurrentUser.OpenSubKey(progIdKey);
            var val = pKey?.GetValue("") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        return null;
    }

    public static string? GetExtensionProgId(string extension)
    {
        extension = NormalizeExtension(extension);

        // 1. UserChoice (Windows 10/11 default association)
        using var userChoiceKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice");
        var progId = userChoiceKey?.GetValue("ProgId") as string;
        if (!string.IsNullOrEmpty(progId)) return progId;

        // 2. HKCU .ext
        using var hkcuExtKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}");
        progId = hkcuExtKey?.GetValue("") as string;
        if (!string.IsNullOrEmpty(progId)) return progId;

        // 3. HKLM .ext
        using var hklmExtKey = Registry.ClassesRoot.OpenSubKey(extension);
        return hklmExtKey?.GetValue("") as string;
    }

    private static string NormalizeExtension(string extension)
    {
        extension = extension.Trim();
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }
        return extension.ToLowerInvariant();
    }
}
