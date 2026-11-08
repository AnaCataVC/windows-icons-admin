namespace WindowsIconsAdmin.Core.Styling;

public class RenderedIconCache
{
    private readonly string _cacheDirectory;

    public RenderedIconCache(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsIconsAdmin",
            "GeneratedIcons");
    }

    public string CacheDirectory => _cacheDirectory;

    public string GetFilePath(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        var safeKey = string.Join("_", cacheKey.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheDirectory, $"{safeKey}.ico");
    }

    public bool TryGet(string cacheKey, out byte[]? icoBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        var path = GetFilePath(cacheKey);

        if (File.Exists(path))
        {
            try
            {
                icoBytes = File.ReadAllBytes(path);
                return true;
            }
            catch
            {
                // Fallback to cache miss on read error
            }
        }

        icoBytes = null;
        return false;
    }

    public string Store(string cacheKey, byte[] icoBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(icoBytes);

        Directory.CreateDirectory(_cacheDirectory);
        var targetPath = GetFilePath(cacheKey);

        if (!File.Exists(targetPath))
        {
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, icoBytes);
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
}
