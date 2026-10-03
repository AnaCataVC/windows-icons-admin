using System.Security.Cryptography;

namespace WindowsIconsAdmin.Core.Storage;

public sealed record StoredIconResult(
    string IconResourceString,
    string? CopiedFileName,
    bool WarnGitRepository,
    bool WarnRemotePath);

public class IconStorageService
{
    public const string DefaultEmbeddedIconName = ".folder_icon.ico";
    private readonly string _centralCacheDirectory;

    public IconStorageService(string? centralCacheDirectory = null)
    {
        _centralCacheDirectory = centralCacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsIconsAdmin",
            "Icons");
    }

    public string CentralCacheDirectory => _centralCacheDirectory;

    public StoredIconResult PrepareIconForFolder(
        string folderPath,
        byte[] icoBytes,
        IconStorageMode mode,
        string? embeddedFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(icoBytes);

        var isRemote = IsRemoteOrNetworkPath(folderPath);
        var hasGit = Directory.Exists(Path.Combine(folderPath, ".git"));

        if (mode == IconStorageMode.PortableEmbedded)
        {
            var fileName = embeddedFileName ?? DefaultEmbeddedIconName;
            var targetPath = Path.Combine(folderPath, fileName);

            // Write icon bytes to the target folder
            File.WriteAllBytes(targetPath, icoBytes);

            // Use relative path for maximum portability across drives/USBs
            var resourceString = $".\\{fileName},0";
            return new StoredIconResult(resourceString, fileName, hasGit, isRemote);
        }
        else
        {
            // Central cache
            Directory.CreateDirectory(_centralCacheDirectory);
            var hash = Convert.ToHexString(SHA256.HashData(icoBytes)).ToLowerInvariant();
            var cacheFilePath = Path.Combine(_centralCacheDirectory, $"{hash}.ico");

            if (!File.Exists(cacheFilePath))
            {
                File.WriteAllBytes(cacheFilePath, icoBytes);
            }

            return new StoredIconResult($"{cacheFilePath},0", null, hasGit, isRemote);
        }
    }

    public static bool IsRemoteOrNetworkPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // UNC paths start with \\ or //
        if (path.StartsWith(@"\\") || path.StartsWith("//"))
        {
            return true;
        }

        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var driveInfo = new DriveInfo(root);
                return driveInfo.DriveType == DriveType.Network;
            }
        }
        catch
        {
            // If drive cannot be queried, assume not remote
        }

        return false;
    }
}
