using WindowsIconsAdmin.Core.Apps;
using WindowsIconsAdmin.Core.History;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.History;

public class AppIconUndoStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storeFile;

    public AppIconUndoStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AppIconUndoStoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storeFile = Path.Combine(_tempDir, "app-undo.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private static AppBatchRecord CreateSampleBatch(string batchId, bool reverted = false)
    {
        var change = new AppIconChange(
            AppId: "app-spotify",
            AppDisplayName: "Spotify",
            TargetPath: @"C:\Users\Test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Spotify.lnk",
            TargetType: IconTargetType.UserStartMenuShortcut,
            PreviousIconLocation: @"C:\Users\Test\AppData\Local\Microsoft\WindowsApps\Spotify.exe",
            PreviousIconIndex: 0,
            OriginalSnapshotPath: null,
            AppliedIconPath: @"C:\Users\Test\AppData\Local\WindowsIconsAdmin\GeneratedIcons\test.ico",
            TimestampUtc: DateTimeOffset.UtcNow);

        return new AppBatchRecord(
            BatchId: batchId,
            TimestampUtc: DateTimeOffset.UtcNow,
            StyleId: "style-soft-lavender",
            StyleName: "Soft Lavender",
            Changes: [change],
            Reverted: reverted);
    }

    [Fact]
    public void Add_AppendsBatch_AndPersistsToDisk()
    {
        var store = new AppIconUndoStore(_storeFile);
        var batch = CreateSampleBatch("b1");

        store.Add(batch);

        Assert.Single(store.GetAll());
        Assert.Equal("b1", store.GetAll()[0].BatchId);

        // Reload from disk
        var reloaded = new AppIconUndoStore(_storeFile);
        Assert.Single(reloaded.GetAll());
        Assert.Equal("b1", reloaded.GetAll()[0].BatchId);
    }

    [Fact]
    public void GetLastPending_ReturnsMostRecentNonRevertedBatch()
    {
        var store = new AppIconUndoStore(_storeFile);
        store.Add(CreateSampleBatch("b1"));
        store.Add(CreateSampleBatch("b2"));

        var pending = store.GetLastPending();
        Assert.NotNull(pending);
        Assert.Equal("b2", pending.BatchId);

        store.MarkReverted("b2");

        var nextPending = store.GetLastPending();
        Assert.NotNull(nextPending);
        Assert.Equal("b1", nextPending.BatchId);
    }

    [Fact]
    public void RollingRetention_EnforcesMaxBatches()
    {
        var store = new AppIconUndoStore(_storeFile);
        for (var i = 0; i < 55; i++)
        {
            store.Add(CreateSampleBatch($"b{i}"));
        }

        var all = store.GetAll();
        Assert.Equal(AppIconUndoStore.MaxBatches, all.Count);
        Assert.Equal("b5", all[0].BatchId);
        Assert.Equal("b54", all[^1].BatchId);
    }

    [Fact]
    public void CorruptedJson_MovesToBak_AndInitializesEmpty()
    {
        File.WriteAllText(_storeFile, "{ invalid json corrupt content");

        var store = new AppIconUndoStore(_storeFile);

        Assert.Empty(store.GetAll());
        Assert.True(File.Exists(_storeFile + ".bak"));
    }
}
