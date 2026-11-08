using WindowsIconsAdmin.Core.Styling;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Styling;

public class RenderedIconCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RenderedIconCache _cache;

    public RenderedIconCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RenderedIconCacheTests_" + Guid.NewGuid().ToString("N"));
        _cache = new RenderedIconCache(_tempDir);
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
    public void StoreAndTryGet_Roundtrip_Works()
    {
        var sampleBytes = new byte[] { 1, 2, 3, 4, 5 };
        var key = "test-key-123";

        Assert.False(_cache.TryGet(key, out _));

        var storedPath = _cache.Store(key, sampleBytes);
        Assert.True(File.Exists(storedPath));

        Assert.True(_cache.TryGet(key, out var retrieved));
        Assert.Equal(sampleBytes, retrieved);
    }
}
