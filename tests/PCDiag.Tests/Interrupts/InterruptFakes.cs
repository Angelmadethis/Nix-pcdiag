using PCDiag.Interrupts;

namespace PCDiag.Tests.Interrupts;

internal sealed class FakeInterruptSnapshotSource : IInterruptSnapshotSource
{
    public InterruptSnapshot Snapshot { get; set; } = new();

    public Task<InterruptSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        => Task.FromResult(Snapshot);
}

internal static class Int
{
    public static InterruptCoreSample Total(
        double? interrupts = null,
        double? dpcs = null,
        double? privileged = null,
        double? cpu = null,
        double? dpcRate = null)
        => new()
        {
            Instance = "_Total",
            InterruptsPerSecond = interrupts,
            DpcsPerSecond = dpcs,
            PrivilegedPercent = privileged,
            ProcessorPercent = cpu,
            DpcRate = dpcRate
        };

    public static InterruptCoreSample Core(
        string id,
        double? interrupts = null,
        double? dpcs = null,
        double? privileged = null,
        double? cpu = null)
        => new()
        {
            Instance = id,
            InterruptsPerSecond = interrupts,
            DpcsPerSecond = dpcs,
            PrivilegedPercent = privileged,
            ProcessorPercent = cpu
        };

    public static InterruptSnapshot Snapshot(InterruptCoreSample? total, params InterruptCoreSample[] cores)
        => new()
        {
            Total = total,
            Cores = cores,
            SampleDurationSeconds = 1.5,
            CountersAvailable = total is not null
                                && (total.InterruptsPerSecond is not null || total.DpcsPerSecond is not null),
            TopologyAvailable = cores.Length > 0,
            InventoryAvailable = true
        };
}