namespace PCDiag.Memory;

/// <summary>
/// Abstraction over the memory snapshot providers (mock seam for tests).
/// Implementations never throw; missing data is surfaced via the snapshot's
/// availability flags.
/// </summary>
public interface IMemorySnapshotSource
{
    MemorySnapshot GetSnapshot();
}