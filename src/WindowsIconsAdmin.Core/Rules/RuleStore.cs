using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsIconsAdmin.Core.Rules;

public sealed class RuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly List<FolderRule> _rules = [];

    public RuleStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Load();
    }

    public IReadOnlyList<FolderRule> GetAll()
    {
        lock (_gate)
        {
            return _rules
                .OrderBy(r => r.Priority)
                .ToList()
                .AsReadOnly();
        }
    }

    public void SaveAll(IEnumerable<FolderRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var list = rules.ToList();
        if (list.Any(r => r is null))
        {
            throw new ArgumentException("Rules cannot contain null elements.", nameof(rules));
        }

        // Normalize sequential priorities based on list order if needed
        var normalized = list
            .Select((r, idx) => r with { Priority = idx + 1 })
            .ToList();

        lock (_gate)
        {
            var previous = _rules.ToList();
            _rules.Clear();
            _rules.AddRange(normalized);
            Commit(previous);
        }
    }

    private void Commit(List<FolderRule> previous)
    {
        try
        {
            Save();
        }
        catch
        {
            _rules.Clear();
            _rules.AddRange(previous);
            throw;
        }
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<List<FolderRule>>(json, JsonOptions);
            if (loaded is null || loaded.Any(r => r is null || string.IsNullOrWhiteSpace(r.Id)))
            {
                throw new JsonException("Invalid rules file content.");
            }

            _rules.AddRange(loaded.OrderBy(r => r.Priority));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            File.Copy(_filePath, _filePath + ".bak", overwrite: true);
            _rules.Clear();
        }
    }

    private void Save()
    {
        var fullPath = Path.GetFullPath(_filePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_rules, JsonOptions));
            if (File.Exists(fullPath))
            {
                File.Replace(temp, fullPath, null);
            }
            else
            {
                File.Move(temp, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
