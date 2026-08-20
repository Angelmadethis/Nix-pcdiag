using System.Threading;

namespace PCDiag.Interrupts;

/// <summary>
/// Supplies an interrupt/DPC activity snapshot from performance counters plus a
/// non-attributed driver/device inventory. Read-only and never throws; unreadable
/// data is reported via availability flags so the check degrades honestly.
/// </summary>
public interface IInterruptSnapshotSource
{
    Task<InterruptSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}