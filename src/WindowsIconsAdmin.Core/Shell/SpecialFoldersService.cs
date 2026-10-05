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
        [SpecialFolderKind.Music] = (new Guid("4BD8D571-6D19-48D3-BE97-422220080E43"), "Música"),
        [SpecialFolderKind.Videos] = (new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC"), "Videos")
    };

    private static readonly Dictionary<SpecialFolderKind, string[]> SpecialFolderClsids = new()
    {
        [SpecialFolderKind.Desktop] =
        [
            "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"
        ],
        [SpecialFolderKind.Downloads] =
        [
            "{088e3905-0323-4b02-9826-5d99428e115f}",
            "{374DE290-123F-4565-9164-39C4925E467B}"
        ],
        [SpecialFolderKind.Documents] =
        [
            "{d3162b92-9365-467a-956b-92703aca08af}",
            "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}",
            "{FDD39AD0-238F-46AF-ADB4-6C85480369C7}"
        ],
        [SpecialFolderKind.Pictures] =
        [
            "{24ad3ad4-a569-4530-98e1-ab02f9417aa8}",
            "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}",
            "{33E28130-4E1E-4676-835A-98395C3BC3BB}"
        ],
        [SpecialFolderKind.Music] =
        [
            "{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de}",
            "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}",
            "{4BD8D571-6D19-48D3-BE97-422220080E43}"
        ],
        [SpecialFolderKind.Videos] =
        [
            "{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a}",
            "{A0953C92-50DC-43bf-BE83-3742FED03C9C}",
            "{18989B1D-99B5-455B-841C-AB7C74E4DDFC}"
        ]
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        nint hToken,
        out nint ppszPath);

    public static IReadOnlyList<string> GetSpecialFolderClsids(SpecialFolderKind kind)
    {
        return SpecialFolderClsids.TryGetValue(kind, out var clsids) ? clsids : Array.Empty<string>();
    }

    public static bool TryGetSpecialFolderKind(string folderPath, out SpecialFolderKind matchedKind)
    {
        matchedKind = default;
        if (string.IsNullOrWhiteSpace(folderPath)) return false;

        string normalizedCandidate;
        try
        {
            normalizedCandidate = Path.GetFullPath(folderPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return false;
        }

        foreach (var kind in Enum.GetValues<SpecialFolderKind>())
        {
            var resolved = ResolveFolderPath(kind);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                var normalizedResolved = Path.GetFullPath(resolved)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedCandidate, normalizedResolved, StringComparison.OrdinalIgnoreCase))
                {
                    matchedKind = kind;
                    return true;
                }
            }

            var fallback = ResolveFallbackFolderPath(kind);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                var normalizedFallback = Path.GetFullPath(fallback)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedCandidate, normalizedFallback, StringComparison.OrdinalIgnoreCase))
                {
                    matchedKind = kind;
                    return true;
                }
            }
        }

        return false;
    }

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

        return ResolveFallbackFolderPath(kind);
    }

    private static string ResolveFallbackFolderPath(SpecialFolderKind kind)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return kind switch
        {
            SpecialFolderKind.Downloads => Path.Combine(userProfile, "Downloads"),
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
