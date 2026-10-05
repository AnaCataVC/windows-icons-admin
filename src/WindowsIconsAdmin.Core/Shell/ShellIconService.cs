using System.Runtime.InteropServices;
using Microsoft.Win32;
using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Safety;

namespace WindowsIconsAdmin.Core.Shell;

public class ShellIconService
{
    // Win32 File Attributes
    public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    public const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

    // Shell Change Notify Events
    public const uint SHCNE_UPDATEDIR = 0x00001000;
    public const uint SHCNE_UPDATEITEM = 0x00002000;
    public const uint SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_IDLIST = 0x0000;
    public const uint SHCNF_PATHW = 0x0005;
    public const uint SHCNF_FLUSH = 0x1000;
    public const uint SHCNF_FLUSHNOWAIT = 0x2000;

    // SHGetSetFolderCustomSettings constants
    private const uint FCSM_ICONFILE = 0x00000010;
    private const uint FCS_FORCEWRITE = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFOLDERCUSTOMSETTINGS
    {
        public uint dwSize;
        public uint dwMask;
        public IntPtr pvid;
        public string? pszWebViewTemplate;
        public uint cchWebViewTemplate;
        public string? pszWebViewTemplateVersion;
        public string? pszInfoTip;
        public uint cchInfoTip;
        public IntPtr pclsid;
        public uint dwFlags;
        public string? pszIconFile;
        public uint cchIconFile;
        public int iIconIndex;
        public string? pszLogo;
        public uint cchLogo;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetFileAttributesW(string lpFileName, uint dwFileAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFileAttributesW(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WritePrivateProfileStringW(
        string? lpAppName,
        string? lpKeyName,
        string? lpString,
        string lpFileName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetSetFolderCustomSettings(
        ref SHFOLDERCUSTOMSETTINGS pfcs,
        string pszPath,
        uint dwReadWrite);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("shell32.dll", EntryPoint = "SHUpdateRecycleBinIcon")]
    public static extern void SHUpdateRecycleBinIcon();

    // CLSIDs for System Icons
    private static readonly Dictionary<SystemIconKind, (string Clsid, string ValueName)> SystemIconClsids = new()
    {
        [SystemIconKind.RecycleBinEmpty] = ("{645FF040-5081-101B-9F08-00AA002F954E}", "empty"),
        [SystemIconKind.RecycleBinFull] = ("{645FF040-5081-101B-9F08-00AA002F954E}", "full"),
        [SystemIconKind.ThisPC] = ("{20D04FE0-3AEA-1069-A2D8-08002B30309D}", ""),
        [SystemIconKind.Network] = ("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", ""),
        [SystemIconKind.UserFiles] = ("{59031a47-3f72-44a7-89c5-5595fe6b30ee}", "")
    };

    public virtual FolderSnapshot ApplyFolderIcon(
        string folderPath,
        string iconResource,
        string? copiedFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(iconResource);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        var iniPath = Path.Combine(folderPath, "desktop.ini");
        var snapshot = CaptureSnapshot(folderPath, copiedFileName);

        var existingContent = File.Exists(iniPath) ? ReadAllTextSafe(iniPath) : null;
        var updatedContent = IniHelper.SetIconResource(existingContent, iconResource);

        // Normalize attributes before writing so Windows doesn't block with WinError 5
        if (File.Exists(iniPath))
        {
            SetAttributes(iniPath, FILE_ATTRIBUTE_NORMAL);
        }

        // Notify windows.storage.dll internal custom settings cache first
        TryApplyNativeFolderCustomSettings(folderPath, iconResource);

        if (File.Exists(iniPath))
        {
            SetAttributes(iniPath, FILE_ATTRIBUTE_NORMAL);
        }

        // Write full content preserving custom sections ([ViewState], LocalizedResourceName, etc.)
        File.WriteAllText(iniPath, updatedContent);
        FlushIniMappingCache(iniPath);

        // Required attributes: Hidden + System on desktop.ini
        SetAttributes(iniPath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);

        // Required attribute on folder: ReadOnly (or System)
        var folderAttrs = GetAttributes(folderPath);
        if ((folderAttrs & FILE_ATTRIBUTE_READONLY) == 0 && (folderAttrs & FILE_ATTRIBUTE_SYSTEM) == 0)
        {
            SetAttributes(folderPath, folderAttrs | FILE_ATTRIBUTE_READONLY);
        }

        // If this folder is a Windows KnownFolder (Desktop, Downloads, Documents, Pictures, Music, Videos),
        // also synchronize its Shell Namespace CLSIDs in HKCU so Windows 11 Home / Quick Access / Nav Pane update.
        if (SpecialFoldersService.TryGetSpecialFolderKind(folderPath, out var specialKind))
        {
            SetSpecialFolderRegistryIcons(specialKind, iconResource);
        }

        // Notify Explorer for this specific folder
        NotifyFolderUpdated(folderPath);

        return snapshot;
    }

    public virtual FolderSnapshot RestoreFolderDefault(string folderPath, bool isKnownFolder = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        var isSpecial = SpecialFoldersService.TryGetSpecialFolderKind(folderPath, out var specialKind);
        var isKnown = isKnownFolder || isSpecial || SystemFolderGuard.IsKnownFolder(folderPath);

        var iniPath = Path.Combine(folderPath, "desktop.ini");
        var snapshot = CaptureSnapshot(folderPath, null);

        if (File.Exists(iniPath))
        {
            SetAttributes(iniPath, FILE_ATTRIBUTE_NORMAL);
            var content = ReadAllTextSafe(iniPath);
            var remaining = IniHelper.RemoveShellClassInfo(content, preserveShellDirectives: isKnown);

            if (remaining is not null)
            {
                File.WriteAllText(iniPath, remaining);
                FlushIniMappingCache(iniPath);
                SetAttributes(iniPath, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);
            }
            else
            {
                File.Delete(iniPath);
                FlushIniMappingCache(iniPath);
            }
        }

        // Also clean up any embedded icon if present (.folder_icon.ico)
        var embeddedIcon = Path.Combine(folderPath, ".folder_icon.ico");
        if (File.Exists(embeddedIcon))
        {
            SetAttributes(embeddedIcon, FILE_ATTRIBUTE_NORMAL);
            File.Delete(embeddedIcon);
        }

        if (!isKnown)
        {
            // Remove ReadOnly / System attributes from regular folder
            var folderAttrs = GetAttributes(folderPath);
            var cleanedAttrs = folderAttrs & ~(FILE_ATTRIBUTE_READONLY | FILE_ATTRIBUTE_SYSTEM);
            if (cleanedAttrs == 0) cleanedAttrs = FILE_ATTRIBUTE_NORMAL;
            SetAttributes(folderPath, cleanedAttrs);
        }

        if (isSpecial)
        {
            RestoreSpecialFolderRegistryIcons(specialKind);
        }

        NotifyFolderUpdated(folderPath);
        return snapshot;
    }

    public virtual void RevertFolder(FolderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Directory.Exists(snapshot.FolderPath))
        {
            return;
        }

        var iniPath = Path.Combine(snapshot.FolderPath, "desktop.ini");

        // Remove copied icon if present and validated (SC-R1, SC-R2)
        if (!string.IsNullOrWhiteSpace(snapshot.CopiedIconFileName))
        {
            var fileValidation = SystemFolderGuard.ValidateEmbeddedFileName(snapshot.CopiedIconFileName);
            if (fileValidation.IsValid)
            {
                var copiedPath = Path.Combine(snapshot.FolderPath, snapshot.CopiedIconFileName);
                if (SystemFolderGuard.IsContainedWithin(snapshot.FolderPath, copiedPath) && File.Exists(copiedPath))
                {
                    SetAttributes(copiedPath, FILE_ATTRIBUTE_NORMAL);
                    File.Delete(copiedPath);
                }
            }
            // If CopiedIconFileName violates safety, RevertFolder skips file deletion and does not throw (SC-R2)
        }

        if (snapshot.HadDesktopIni)
        {
            if (File.Exists(iniPath))
            {
                SetAttributes(iniPath, FILE_ATTRIBUTE_NORMAL);
            }

            File.WriteAllText(iniPath, snapshot.PreviousDesktopIniContent ?? string.Empty);
            var iniAttrs = snapshot.PreviousDesktopIniAttributes ?? (FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);
            SetAttributes(iniPath, iniAttrs);
        }
        else
        {
            if (File.Exists(iniPath))
            {
                SetAttributes(iniPath, FILE_ATTRIBUTE_NORMAL);
                File.Delete(iniPath);
            }
        }

        // Restore folder attributes
        SetAttributes(snapshot.FolderPath, snapshot.PreviousFolderAttributes);
        NotifyFolderUpdated(snapshot.FolderPath);
    }

    public virtual void NotifyBatchCompleted()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSHNOWAIT, IntPtr.Zero, IntPtr.Zero);
    }

    public static void NotifyAssociationChanged()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSHNOWAIT, IntPtr.Zero, IntPtr.Zero);
    }

    public static void ValidateLocalIconPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var cleanPath = path.Contains(',') ? path.Split(',')[0].Trim('"') : path.Trim('"');

        if (cleanPath.StartsWith(@"\\") || cleanPath.StartsWith("//"))
        {
            throw new ArgumentException("Remote UNC network paths are not permitted for security reasons.", nameof(path));
        }

        if (!Path.IsPathRooted(cleanPath))
        {
            throw new ArgumentException("Icon path must be a fully rooted local file path.", nameof(path));
        }

        var ext = Path.GetExtension(cleanPath);
        if (!ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unsupported icon extension: '{ext}'. Allowed: .ico, .png, .dll, .exe", nameof(path));
        }
    }

    public static string FormatIconResourcePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = path.Trim().Trim('"');

        if (path.Contains(','))
        {
            return path;
        }

        return $"{path},0";
    }

    public virtual void SetSystemIcon(SystemIconKind kind, string iconPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ValidateLocalIconPath(iconPath);
        var formattedPath = FormatIconResourcePath(iconPath);

        if (!SystemIconClsids.TryGetValue(kind, out var info))
        {
            throw new NotSupportedException($"Unsupported system icon kind: {kind}");
        }

        var keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{info.Clsid}\DefaultIcon";
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(info.ValueName, formattedPath, RegistryValueKind.String);

        if (kind == SystemIconKind.RecycleBinEmpty)
        {
            key.SetValue("", formattedPath, RegistryValueKind.String);
        }

        // Mirror to Themes\DefaultIcon for Windows 10 / 11 themes persistence
        using (var themeKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\DefaultIcon", writable: true))
        {
            if (kind == SystemIconKind.RecycleBinEmpty)
            {
                themeKey.SetValue(info.Clsid, formattedPath, RegistryValueKind.String);
                themeKey.SetValue("empty", formattedPath, RegistryValueKind.String);
            }
            else if (kind == SystemIconKind.RecycleBinFull)
            {
                themeKey.SetValue("full", formattedPath, RegistryValueKind.String);
            }
            else
            {
                themeKey.SetValue(info.Clsid, formattedPath, RegistryValueKind.String);
            }
        }

        if (kind == SystemIconKind.RecycleBinEmpty || kind == SystemIconKind.RecycleBinFull)
        {
            SHUpdateRecycleBinIcon();
        }

        NotifyBatchCompleted();
    }

    public virtual void RestoreSystemIcon(SystemIconKind kind)
    {
        if (!SystemIconClsids.TryGetValue(kind, out var info))
        {
            throw new NotSupportedException($"Unsupported system icon kind: {kind}");
        }

        var keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{info.Clsid}\DefaultIcon";
        using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
        {
            if (key != null)
            {
                if (string.IsNullOrEmpty(info.ValueName))
                {
                    key.DeleteValue("", false);
                }
                else
                {
                    key.DeleteValue(info.ValueName, false);
                    if (kind == SystemIconKind.RecycleBinEmpty)
                    {
                        key.DeleteValue("", false);
                    }
                }
            }
        }

        // Clean Themes\DefaultIcon mirrors
        using (var themeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\DefaultIcon", writable: true))
        {
            if (themeKey != null)
            {
                if (kind == SystemIconKind.RecycleBinEmpty)
                {
                    themeKey.DeleteValue(info.Clsid, false);
                    themeKey.DeleteValue("empty", false);
                }
                else if (kind == SystemIconKind.RecycleBinFull)
                {
                    themeKey.DeleteValue("full", false);
                }
                else
                {
                    themeKey.DeleteValue(info.Clsid, false);
                }
            }
        }

        if (kind == SystemIconKind.RecycleBinEmpty || kind == SystemIconKind.RecycleBinFull)
        {
            SHUpdateRecycleBinIcon();
        }

        NotifyBatchCompleted();
    }

    public virtual string? GetCurrentSystemIcon(SystemIconKind kind)
    {
        if (!SystemIconClsids.TryGetValue(kind, out var info)) return null;

        var keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{info.Clsid}\DefaultIcon";
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        if (key == null) return null;

        return key.GetValue(info.ValueName) as string;
    }

    public virtual void SetSpecialFolderRegistryIcons(SpecialFolderKind kind, string iconPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ValidateLocalIconPath(iconPath);
        var formatted = FormatIconResourcePath(iconPath);

        var clsids = SpecialFoldersService.GetSpecialFolderClsids(kind);
        foreach (var clsid in clsids)
        {
            using (var expKey = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{clsid}\DefaultIcon", writable: true))
            {
                expKey.SetValue("", formatted, RegistryValueKind.String);
            }

            using (var clsKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{clsid}\DefaultIcon", writable: true))
            {
                clsKey.SetValue("", formatted, RegistryValueKind.String);
            }

            using (var themeKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\DefaultIcon", writable: true))
            {
                themeKey.SetValue(clsid, formatted, RegistryValueKind.String);
            }
        }
    }

    public virtual void RestoreSpecialFolderRegistryIcons(SpecialFolderKind kind)
    {
        var clsids = SpecialFoldersService.GetSpecialFolderClsids(kind);
        foreach (var clsid in clsids)
        {
            using (var expKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{clsid}", writable: true))
            {
                expKey?.DeleteSubKeyTree("DefaultIcon", false);
            }

            using (var clsKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{clsid}", writable: true))
            {
                clsKey?.DeleteSubKeyTree("DefaultIcon", false);
            }

            using (var themeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\DefaultIcon", writable: true))
            {
                themeKey?.DeleteValue(clsid, false);
            }
        }
    }

    public virtual string? GetCurrentSpecialFolderRegistryIcon(SpecialFolderKind kind)
    {
        var clsids = SpecialFoldersService.GetSpecialFolderClsids(kind);
        foreach (var clsid in clsids)
        {
            using var expKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{clsid}\DefaultIcon");
            var val = expKey?.GetValue("") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }
        return null;
    }

    public virtual void SetDefaultFolderIcon(string iconPath, bool machineWide = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ValidateLocalIconPath(iconPath);
        var formatted = FormatIconResourcePath(iconPath);

        // Per-user overrides (Folder, Directory, and Explorer Shell Icons indices 3 & 4)
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Folder\DefaultIcon", writable: true))
        {
            key.SetValue("", formatted, RegistryValueKind.String);
        }

        using (var dirKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\DefaultIcon", writable: true))
        {
            dirKey.SetValue("", formatted, RegistryValueKind.String);
        }

        using (var hkcuShellIcons = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons", writable: true))
        {
            hkcuShellIcons.SetValue("3", formatted, RegistryValueKind.String);
            hkcuShellIcons.SetValue("4", formatted, RegistryValueKind.String);
        }

        if (machineWide)
        {
            SetHklmShellIconsValues(("3", formatted), ("4", formatted));
        }

        NotifyBatchCompleted();
        TryRunIe4uinit();
    }

    public virtual void RestoreDefaultFolderIcon(bool machineWide = false)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Folder", writable: true))
        {
            key?.DeleteSubKeyTree("DefaultIcon", false);
        }

        using (var dirKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory", writable: true))
        {
            dirKey?.DeleteSubKeyTree("DefaultIcon", false);
        }

        using (var hkcuShellIcons = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons", writable: true))
        {
            hkcuShellIcons?.DeleteValue("3", false);
            hkcuShellIcons?.DeleteValue("4", false);
        }

        if (machineWide)
        {
            DeleteHklmShellIconsValues("3", "4");
        }

        NotifyBatchCompleted();
        TryRunIe4uinit();
    }

    public virtual string? GetCurrentDefaultFolderIcon(bool machineWide = false)
    {
        if (machineWide)
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons");
            var val = hklmKey?.GetValue("3") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        using (var hkcuShellIcons = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons"))
        {
            var val = hkcuShellIcons?.GetValue("3") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Folder\DefaultIcon");
        return key?.GetValue("") as string;
    }

    public virtual void SetDefaultFileIcon(string iconPath, bool machineWide = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ValidateLocalIconPath(iconPath);
        var formatted = FormatIconResourcePath(iconPath);

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Unknown\DefaultIcon", writable: true))
        {
            key.SetValue("", formatted, RegistryValueKind.String);
        }

        using (var hkcuShellIcons = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons", writable: true))
        {
            hkcuShellIcons.SetValue("0", formatted, RegistryValueKind.String);
        }

        if (machineWide)
        {
            SetHklmShellIconsValues(("0", formatted));
        }

        NotifyBatchCompleted();
        TryRunIe4uinit();
    }

    public virtual void RestoreDefaultFileIcon(bool machineWide = false)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Unknown", writable: true))
        {
            key?.DeleteSubKeyTree("DefaultIcon", false);
        }

        using (var hkcuShellIcons = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons", writable: true))
        {
            hkcuShellIcons?.DeleteValue("0", false);
        }

        if (machineWide)
        {
            DeleteHklmShellIconsValues("0");
        }

        NotifyBatchCompleted();
        TryRunIe4uinit();
    }

    public virtual string? GetCurrentDefaultFileIcon(bool machineWide = false)
    {
        if (machineWide)
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons");
            var val = hklmKey?.GetValue("0") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        using (var hkcuShellIcons = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons"))
        {
            var val = hkcuShellIcons?.GetValue("0") as string;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Unknown\DefaultIcon");
        return key?.GetValue("") as string;
    }

    public virtual void RestoreAllSystemAndDefaultIcons()
    {
        foreach (var kind in Enum.GetValues<SystemIconKind>())
        {
            RestoreSystemIcon(kind);
        }

        foreach (var specialKind in Enum.GetValues<SpecialFolderKind>())
        {
            RestoreSpecialFolderRegistryIcons(specialKind);
        }

        RestoreDefaultFolderIcon(machineWide: false);
        RestoreDefaultFileIcon(machineWide: false);
        NotifyBatchCompleted();
        TryRunIe4uinit();
    }

    public virtual void RefreshExplorerIconCache(bool restartExplorer = false)
    {
        TryRunIe4uinit();
        NotifyBatchCompleted();

        if (restartExplorer)
        {
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("explorer"))
                {
                    try { proc.Kill(); } catch { }
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore if Explorer restart fails
            }
        }
    }

    private static void SetHklmShellIconsValues(params (string Name, string Value)[] entries)
    {
        const string subKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
        try
        {
            using var hklmKey = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true);
            foreach (var (name, value) in entries)
            {
                hklmKey.SetValue(name, value, RegistryValueKind.String);
            }
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Fallback to UAC-elevated reg.exe so standard-user execution can elevate cleanly
        }

        var commands = entries.Select(e =>
            $"reg add \"HKLM\\{subKeyPath}\" /v \"{e.Name}\" /t REG_SZ /d \"{e.Value}\" /f");
        RunElevatedCmd(string.Join(" & ", commands));
    }

    private static void DeleteHklmShellIconsValues(params string[] valueNames)
    {
        const string subKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
        try
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: true);
            if (hklmKey != null)
            {
                foreach (var name in valueNames)
                {
                    hklmKey.DeleteValue(name, false);
                }
            }
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Fallback to UAC-elevated reg.exe
        }

        var commands = valueNames.Select(name =>
            $"reg delete \"HKLM\\{subKeyPath}\" /v \"{name}\" /f");
        RunElevatedCmd(string.Join(" & ", commands));
    }

    private static void RunElevatedCmd(string commandLine)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {commandLine}",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        proc?.WaitForExit(15000);
    }

    private static void TryRunIe4uinit()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ie4uinit.exe",
                Arguments = "-show",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch
        {
            // Ignore if ie4uinit is unavailable
        }
    }

    private static void TryApplyNativeFolderCustomSettings(string folderPath, string iconResource)
    {
        try
        {
            ParseIconResource(iconResource, out var iconFilePath, out var iconIndex);
            var fcs = new SHFOLDERCUSTOMSETTINGS
            {
                dwSize = (uint)Marshal.SizeOf<SHFOLDERCUSTOMSETTINGS>(),
                dwMask = FCSM_ICONFILE,
                pszIconFile = iconFilePath,
                cchIconFile = 0,
                iIconIndex = iconIndex
            };
            SHGetSetFolderCustomSettings(ref fcs, folderPath, FCS_FORCEWRITE);
        }
        catch
        {
            // Best-effort cache notification for windows.storage.dll
        }
    }

    private static void ParseIconResource(string iconResource, out string iconFilePath, out int iconIndex)
    {
        var trimmed = iconResource.Trim();
        var lastComma = trimmed.LastIndexOf(',');
        if (lastComma > 0 && int.TryParse(trimmed[(lastComma + 1)..].Trim(), out var parsedIndex))
        {
            iconFilePath = trimmed[..lastComma].Trim().Trim('"');
            iconIndex = parsedIndex;
        }
        else
        {
            iconFilePath = trimmed.Trim('"');
            iconIndex = 0;
        }
    }

    private static void FlushIniMappingCache(string iniPath)
    {
        try
        {
            WritePrivateProfileStringW(null, null, null, iniPath);
        }
        catch
        {
            // Best-effort Win32 INI cache flush
        }
    }

    private static FolderSnapshot CaptureSnapshot(string folderPath, string? copiedFileName)
    {
        var iniPath = Path.Combine(folderPath, "desktop.ini");
        var hadIni = File.Exists(iniPath);
        string? content = null;
        uint? iniAttrs = null;

        if (hadIni)
        {
            content = ReadAllTextSafe(iniPath);
            var a = GetAttributes(iniPath);
            if (a != INVALID_FILE_ATTRIBUTES) iniAttrs = a;
        }

        var fAttrs = GetAttributes(folderPath);
        if (fAttrs == INVALID_FILE_ATTRIBUTES) fAttrs = FILE_ATTRIBUTE_DIRECTORY;

        return new FolderSnapshot(
            folderPath,
            hadIni,
            content,
            fAttrs,
            iniAttrs,
            copiedFileName);
    }

    private static string ReadAllTextSafe(string path)
    {
        // If file has hidden/system, File.ReadAllText can read, but normalization avoids locks
        return File.ReadAllText(path);
    }

    private static uint GetAttributes(string path) => GetFileAttributesW(path);

    private static void SetAttributes(string path, uint attrs) => SetFileAttributesW(path, attrs);

    private static void NotifyFolderUpdated(string folderPath)
    {
        var flags = SHCNF_PATHW | SHCNF_FLUSHNOWAIT;
        var ptr = Marshal.StringToHGlobalUni(folderPath);
        try
        {
            SHChangeNotify(SHCNE_UPDATEITEM, flags, ptr, IntPtr.Zero);
            SHChangeNotify(SHCNE_UPDATEDIR, flags, ptr, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        var parentDir = Path.GetDirectoryName(folderPath.TrimEnd('\\', '/'));
        if (!string.IsNullOrEmpty(parentDir))
        {
            var parentPtr = Marshal.StringToHGlobalUni(parentDir);
            try
            {
                SHChangeNotify(SHCNE_UPDATEDIR, flags, parentPtr, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(parentPtr);
            }
        }
    }
}
