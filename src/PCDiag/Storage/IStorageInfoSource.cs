namespace PCDiag.Storage;

/// <summary>
/// Abstraction over the storage providers (mock seam for tests). Implementations never
/// throw; missing data is surfaced through the snapshot's availability flags. Latency
/// sampling may take a short time (passive, non-destructive).
/// </summary>
public interface IStorageInfoSource
{
    Task<StorageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}