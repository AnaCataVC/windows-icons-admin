namespace WindowsIconsAdmin.Core.History;

public sealed record BatchRecord(
    string BatchId,
    DateTimeOffset TimestampUtc,
    OperationKind Kind,
    string? SourceIconPath,
    IReadOnlyList<FolderSnapshot> Folders,
    bool Reverted)
{
    public bool Equals(BatchRecord? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return BatchId == other.BatchId
            && TimestampUtc == other.TimestampUtc
            && Kind == other.Kind
            && SourceIconPath == other.SourceIconPath
            && Reverted == other.Reverted
            && (ReferenceEquals(Folders, other.Folders)
                || (Folders is not null && other.Folders is not null && Folders.SequenceEqual(other.Folders)));
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BatchId);
        hash.Add(TimestampUtc);
        hash.Add(Kind);
        hash.Add(SourceIconPath);
        hash.Add(Reverted);
        if (Folders is not null)
        {
            foreach (var folder in Folders)
            {
                hash.Add(folder);
            }
        }

        return hash.ToHashCode();
    }
}
