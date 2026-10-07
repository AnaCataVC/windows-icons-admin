using System.Text.Json;

namespace WindowsIconsAdmin.Core.Settings;

public class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly object _gate = new();
    private AppSettings _current;

    public AppSettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsIconsAdmin",
            "settings.json");
        _current = Load();
    }

    public string FilePath => _filePath;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            _current = settings;
            Persist();
        }
    }

    public void Update(Func<AppSettings, AppSettings> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);
        lock (_gate)
        {
            _current = updateAction(_current);
            Persist();
        }
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            try
            {
                File.Copy(_filePath, _filePath + ".bak", overwrite: true);
            }
            catch
            {
                // Best-effort backup of corrupted file
            }
            return new AppSettings();
        }
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var temp = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_current, JsonOptions));
            if (File.Exists(_filePath))
            {
                File.Replace(temp, _filePath, null);
            }
            else
            {
                File.Move(temp, _filePath);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // Ignore temp deletion failure
                }
            }
        }
    }
}
