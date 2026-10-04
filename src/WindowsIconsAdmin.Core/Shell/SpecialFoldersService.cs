using System.Runtime.InteropServices;

namespace WindowsIconsAdmin.Core.Shell;

public record SpecialFolderInfo(
    SpecialFolderKind Kind,
    string DisplayName,
    string FolderPath,
    bool Exists,
    bool IsCloudSynced);

public class SpecialFoldersService
{
    private static readonly Dictionary<SpecialFolderKind, (Guid Guid, string Name)> KnownFolderGuids = new()
    {
        [SpecialFolderKind.Downloads] = (new Guid("374DE290-123F-4565-9164-39C4925E467B"), "Descargas"),
        [SpecialFolderKind.Documents] = (new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7"), "Documentos"),
        [SpecialFolderKind.Desktop] = (new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641"), "Escritorio"),
        [SpecialFolderKind.Pictures] = (new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB"), "Imágenes"),
        [SpecialFolderKind.Music] = (new Guid("4BD8D570-5094-4F3F-A765-67C8772C5AB3"), "Música"),
        [SpecialFolderKind.Videos] = (new Guid("1898EBFC-2748-4B0B-8A2A-E62FDE9E504E"), "Videos")
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        nint hToken,
        out nint ppszPath);

    public static string ResolveFolderPath(SpecialFolderKind kind)
    {
        if (KnownFolderGuids.TryGetValue(kind, out var info))
        {
            var hr = SHGetKnownFolderPath(info.Guid, 0, nint.Zero, out var ppszPath);
            if (hr == 0 && ppszPath != nint.Zero)
            {
                try
                {
                    var path = Marshal.PtrToStringUni(ppszPath);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(ppszPath);
                }
            }
        }

        // Fallback to Environment.SpecialFolder
        return kind switch
        {
            SpecialFolderKind.Downloads => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            SpecialFolderKind.Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            SpecialFolderKind.Desktop => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            SpecialFolderKind.Pictures => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            SpecialFolderKind.Music => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            SpecialFolderKind.Videos => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static IReadOnlyList<SpecialFolderInfo> GetAllSpecialFolders()
    {
        var list = new List<SpecialFolderInfo>();
        foreach (var (kind, info) in KnownFolderGuids)
        {
            var path = ResolveFolderPath(kind);
            var exists = Directory.Exists(path);
            var isCloudSynced = exists && (
                path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("Dropbox", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("iCloudDrive", StringComparison.OrdinalIgnoreCase));

            list.Add(new SpecialFolderInfo(kind, info.Name, path, exists, isCloudSynced));
        }

        return list;
    }
}
