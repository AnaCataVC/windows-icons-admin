namespace WindowsIconsAdmin.Core.History;

public sealed record AppBatchRecord(
    string BatchId,
    DateTimeOffset TimestampUtc,
    string StyleId,
    string? StyleName,
    IReadOnlyList<AppIconChange> Changes,
    bool Reverted);
