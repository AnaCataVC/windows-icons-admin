namespace WindowsIconsAdmin.Core.Styling;

public class StyleStore
{
    private readonly string _stylesDirectory;

    public StyleStore(string? stylesDirectory = null)
    {
        _stylesDirectory = stylesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsIconsAdmin",
            "Styles");
    }

    public string StylesDirectory => _stylesDirectory;

    public void Save(IconStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrWhiteSpace(style.Id))
        {
            throw new ArgumentException("Style Id cannot be empty.", nameof(style));
        }

        Directory.CreateDirectory(_stylesDirectory);
        var filePath = GetStyleFilePath(style.Id);
        var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(tempPath, style.ToJson());
            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public IconStyle? Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var filePath = GetStyleFilePath(id);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return IconStyle.FromJson(json);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<IconStyle> GetAll()
    {
        if (!Directory.Exists(_stylesDirectory))
        {
            return [IconStyle.CreateDefault()];
        }

        var results = new List<IconStyle>();
        foreach (var file in Directory.EnumerateFiles(_stylesDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var style = IconStyle.FromJson(json);
                results.Add(style);
            }
            catch
            {
                // Skip corrupted individual files
            }
        }

        if (results.Count == 0)
        {
            results.Add(IconStyle.CreateDefault());
        }

        return results.AsReadOnly();
    }

    public IReadOnlyList<IconStyle> GetAllWithPresets()
    {
        var existing = GetAll().ToList();
        var seenIds = new HashSet<string>(existing.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);

        var presets = IconStyle.GetCuratedPresets();
        foreach (var preset in presets)
        {
            if (!seenIds.Contains(preset.Id))
            {
                existing.Add(preset);
                seenIds.Add(preset.Id);
            }
        }

        return existing.AsReadOnly();
    }

    public bool Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var filePath = GetStyleFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        return false;
    }

    private string GetStyleFilePath(string id)
    {
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_stylesDirectory, $"{safeId}.json");
    }
}
