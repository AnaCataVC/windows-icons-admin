using WindowsIconsAdmin.Core.History;
using WindowsIconsAdmin.Core.Styling;

namespace WindowsIconsAdmin.Core.Apps;

public sealed record AppBatchResult(
    int AppliedCount,
    int SkippedCount,
    int ErrorCount,
    AppBatchRecord? BatchRecord,
    IReadOnlyList<string> ErrorMessages);

public interface IAppIconService
{
    AppBatchResult ApplyBatch(
        IReadOnlyList<(InstalledApp App, IconStyle Style, byte[] IcoBytes)> batchItems,
        string? batchId = null);

    bool RevertLastBatch();
    bool RevertBatch(string batchId);
    void RevertSingleChange(AppIconChange change);
}
