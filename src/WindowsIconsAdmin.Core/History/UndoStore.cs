using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsIconsAdmin.Core.History;

public sealed class UndoStore
{
    public const int MaxBatches = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly List<BatchRecord> _batches = [];

    public UndoStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Load();
    }

    public void Add(BatchRecord batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (string.IsNullOrWhiteSpace(batch.BatchId))
        {
            throw new ArgumentException("BatchId cannot be null or blank.", nameof(batch));
        }

        ArgumentNullException.ThrowIfNull(batch.Folders, nameof(batch));
        var folders = batch.Folders.ToList();
        if (folders.Any(f => f is null))
        {
            throw new ArgumentException("Folders cannot contain null elements.", nameof(batch));
        }

        var copy = batch with { Folders = folders.AsReadOnly() };
        lock (_gate)
        {
            if (_batches.Any(b => b.BatchId == copy.BatchId))
            {
                throw new ArgumentException($"A batch with id '{copy.BatchId}' already exists.", nameof(batch));
            }

            var previous = _batches.ToList();
            _batches.Add(copy);
            if (_batches.Count > MaxBatches)
            {
                _batches.RemoveRange(0, _batches.Count - MaxBatches);
            }

            Commit(previous);
        }
    }

    public BatchRecord? GetLastPending()
    {
        lock (_gate)
        {
            return _batches.LastOrDefault(b => !b.Reverted);
        }
    }

    public void MarkReverted(string batchId)
    {
        lock (_gate)
        {
            var index = _batches.FindIndex(b => b.BatchId == batchId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Batch '{batchId}' was not found.");
            }

            if (_batches[index].Reverted)
            {
                return;
            }

            var previous = _batches.ToList();
            _batches[index] = _batches[index] with { Reverted = true };
            Commit(previous);
        }
    }

    public IReadOnlyList<BatchRecord> GetAll()
    {
        lock (_gate)
        {
            return _batches.ToList().AsReadOnly();
        }
    }

    private void Commit(List<BatchRecord> previous)
    {
        try
        {
            Save();
        }
        catch
        {
            _batches.Clear();
            _batches.AddRange(previous);
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
            var loaded = JsonSerializer.Deserialize<List<BatchRecord>>(json, JsonOptions);
            if (loaded is null || loaded.Any(b => b is null || b.Folders is null || b.Folders.Any(f => f is null)))
            {
                throw new JsonException("Invalid content.");
            }

            _batches.AddRange(loaded);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            File.Copy(_filePath, _filePath + ".bak", overwrite: true);
            _batches.Clear();
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
            File.WriteAllText(temp, JsonSerializer.Serialize(_batches, JsonOptions));
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
