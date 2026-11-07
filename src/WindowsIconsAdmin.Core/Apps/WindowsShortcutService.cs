using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIconsAdmin.Core.Apps;

public class WindowsShortcutService : IShortcutService
{
    private const uint STGM_READ = 0x00000000;
    private const uint STGM_READWRITE = 0x00000002;

    public virtual bool ShortcutExists(string shortcutPath)
    {
        return !string.IsNullOrWhiteSpace(shortcutPath) && File.Exists(shortcutPath);
    }

    public virtual (string TargetPath, string? IconLocation, int IconIndex) ReadShortcut(string shortcutPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        if (!File.Exists(shortcutPath))
        {
            throw new FileNotFoundException("Shortcut file not found.", shortcutPath);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return (shortcutPath, null, 0);
        }

        var link = (IShellLinkW)new ShellLinkClass();
        var persist = (IPersistFile)link;

        persist.Load(shortcutPath, STGM_READ);

        var targetSb = new StringBuilder(1024);
        var iconPathSb = new StringBuilder(1024);

        try
        {
            link.GetPath(targetSb, targetSb.Capacity, out _, 0);
        }
        catch
        {
            // Some special shell shortcuts do not have a raw target path
        }

        link.GetIconLocation(iconPathSb, iconPathSb.Capacity, out var iconIndex);

        var iconLocation = iconPathSb.ToString();
        if (string.IsNullOrWhiteSpace(iconLocation))
        {
            iconLocation = null;
        }

        return (targetSb.ToString(), iconLocation, iconIndex);
    }

    public virtual void SetShortcutIcon(string shortcutPath, string iconLocation, int iconIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        if (!File.Exists(shortcutPath))
        {
            throw new FileNotFoundException("Shortcut file not found.", shortcutPath);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var link = (IShellLinkW)new ShellLinkClass();
        var persist = (IPersistFile)link;

        persist.Load(shortcutPath, STGM_READWRITE);
        link.SetIconLocation(iconLocation ?? string.Empty, iconIndex);
        persist.Save(shortcutPath, true);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkClass { }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
