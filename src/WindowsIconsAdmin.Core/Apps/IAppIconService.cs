using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin.Core.Apps;

public sealed record AppBatchResult(
    int AppliedCount,
    int SkippedCount,
    int ErrorCount,
    bool WasCancelled,
    AppBatchRecord? BatchRecord,
    IReadOnlyList<string> ErrorMessages);

public interface IAppIconService
{
    AppBatchResult ApplyBatch(
        IReadOnlyList<(InstalledApp App, IconStyle Style, byte[] IcoBytes)> batchItems,
        string? batchId = null,
        IProgress<(int Current, int Total, string Status)>? progress = null,
        CancellationToken cancellationToken = default);

    bool RevertLastBatch();
    bool RevertBatch(string batchId);
    int RevertAll();
    bool RevertApp(string appId);
    void RevertSingleChange(AppIconChange change);
}
