using WindowsIconsAdmin.Core.History;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.History;

public class OriginalSnapshotStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OriginalSnapshotStore _store;

    public OriginalSnapshotStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OriginalSnapshotStoreTests_" + Guid.NewGuid().ToString("N"));
        _store = new OriginalSnapshotStore(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void SaveAndTryGet_Works()
    {
        var sampleBytes = new byte[] { 9, 8, 7, 6 };
        var appId = "app-test";

        Assert.False(_store.SnapshotExists(appId));

        var path = _store.SaveSnapshot(appId, sampleBytes);
        Assert.True(File.Exists(path));
        Assert.True(_store.SnapshotExists(appId));

        Assert.True(_store.TryGetSnapshot(appId, out var retrieved));
        Assert.Equal(sampleBytes, retrieved);
    }
}
