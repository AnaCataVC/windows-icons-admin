namespace WindowsIconsAdmin.Core.History;

public class OriginalSnapshotStore
{
    private readonly string _snapshotsDirectory;

    public OriginalSnapshotStore(string? snapshotsDirectory = null)
    {
        _snapshotsDirectory = snapshotsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsIconsAdmin",
            "OriginalSnapshots");
    }

    public string SnapshotsDirectory => _snapshotsDirectory;

    public string GetSnapshotPath(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        var safeId = string.Join("_", appId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_snapshotsDirectory, $"{safeId}.ico");
    }

    public bool SnapshotExists(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return File.Exists(GetSnapshotPath(appId));
    }

    public string SaveSnapshot(string appId, byte[] iconBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(iconBytes);

        Directory.CreateDirectory(_snapshotsDirectory);
        var targetPath = GetSnapshotPath(appId);

        if (!File.Exists(targetPath))
        {
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, iconBytes);
                File.Move(tempPath, targetPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        return targetPath;
    }

    public bool TryGetSnapshot(string appId, out byte[]? iconBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        var path = GetSnapshotPath(appId);

        if (File.Exists(path))
        {
            try
            {
                iconBytes = File.ReadAllBytes(path);
                return true;
            }
            catch
            {
                // Fallback
            }
        }

        iconBytes = null;
        return false;
    }
}
