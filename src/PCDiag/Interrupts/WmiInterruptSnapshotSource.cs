using System.Diagnostics;
using PCDiag.Infrastructure;

namespace PCDiag.Interrupts;

/// <summary>
/// Reads interrupt/DPC activity from Win32_PerfRawData_PerfOS_Processor by taking two
/// passive samples ~1.5s apart and computing deltas, plus a non-attributed inventory of
/// loaded drivers and PnP devices. Only counters that remain reliable on modern Windows
/// are used; the deprecated PercentDPCTime/PercentInterruptTime counters (always zero
/// since Windows 8) are never read. This measures activity rates, never true DPC latency.
/// </summary>
public sealed class WmiInterruptSnapshotSource : IInterruptSnapshotSource
{
    private const string PerfProcessorQuery =
        "SELECT Name, InterruptsPersec, DPCsQueuedPersec, DPCRate, " +
        "PercentPrivilegedTime, PercentProcessorTime, Timestamp_Sys100NS " +
        "FROM Win32_PerfRawData_PerfOS_Processor";

    private readonly TimeSpan _sampleInterval;

    public WmiInterruptSnapshotSource(TimeSpan? sampleInterval = null)
        => _sampleInterval = sampleInterval ?? TimeSpan.FromMilliseconds(1500);

    public Task<InterruptSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => ReadSnapshot(cancellationToken), cancellationToken);

    private InterruptSnapshot ReadSnapshot(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var first = QueryProcessorRows();
        cancellationToken.ThrowIfCancellationRequested();
        Thread.Sleep(_sampleInterval);
        var second = QueryProcessorRows();
        timer.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        var elapsed = timer.Elapsed.TotalSeconds;
        var samples = BuildSamples(first, second, elapsed);

        var total = samples.FirstOrDefault(s => s.Instance.Equals("_Total", StringComparison.OrdinalIgnoreCase));
        var cores = samples
            .Where(s => !s.Instance.Equals("_Total", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Instance, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var inventory = ReadInventory();

        return new InterruptSnapshot
        {
            Cores = cores,
            Total = total,
            SampleDurationSeconds = elapsed,
            CountersAvailable = total is not null
                               && (total.InterruptsPerSecond is not null || total.DpcsPerSecond is not null),
            TopologyAvailable = cores.Count > 0,
            LoadedDrivers = inventory.Drivers,
            Devices = inventory.Devices,
            InventoryAvailable = inventory.Available
        };
    }

    private static IReadOnlyList<ProcessorRawRow> QueryProcessorRows()
    {
        return WmiQuery.Query(PerfProcessorQuery)
            .Select(r => new ProcessorRawRow
            {
                Name = WmiQuery.GetString(r, "Name") ?? "_Total",
                Interrupts = WmiQuery.GetInt64(r, "InterruptsPersec"),
                Dpcs = WmiQuery.GetInt64(r, "DPCsQueuedPersec"),
                DpcRate = WmiQuery.GetInt64(r, "DPCRate"),
                Privileged = WmiQuery.GetInt64(r, "PercentPrivilegedTime"),
                Processor = WmiQuery.GetInt64(r, "PercentProcessorTime"),
                Timestamp100Ns = WmiQuery.GetInt64(r, "Timestamp_Sys100NS")
            })
            .ToList();
    }

    private static IReadOnlyList<InterruptCoreSample> BuildSamples(
        IReadOnlyList<ProcessorRawRow> first,
        IReadOnlyList<ProcessorRawRow> second,
        double elapsedSeconds)
    {
        var samples = new List<InterruptCoreSample>();

        foreach (var row2 in second)
        {
            var row1 = first.FirstOrDefault(f => string.Equals(f.Name, row2.Name, StringComparison.OrdinalIgnoreCase));
            if (row1 is null)
                continue;

            var instance = row2.Name;
            samples.Add(new InterruptCoreSample
            {
                Instance = instance,
                InterruptsPerSecond = DeltaRate(row1.Interrupts, row2.Interrupts, elapsedSeconds),
                DpcsPerSecond = DeltaRate(row1.Dpcs, row2.Dpcs, elapsedSeconds),
                DpcRate = row2.DpcRate,
                PrivilegedPercent = Percent(row1.Privileged, row1.Timestamp100Ns, row2.Privileged, row2.Timestamp100Ns),
                ProcessorPercent = Percent(row1.Processor, row1.Timestamp100Ns, row2.Processor, row2.Timestamp100Ns)
            });
        }

        return samples;
    }

    private static double? DeltaRate(long? a, long? b, double elapsedSeconds)
        => a is long x && b is long y && elapsedSeconds > 0
            ? (y - x) / elapsedSeconds
            : null;

    private static double? Percent(long? a, long? aBase, long? b, long? bBase)
    {
        if (a is not long av || b is not long bv || aBase is not long ab || bBase is not long bb)
            return null;
        var deltaValue = bv - av;
        var deltaBase = bb - ab;
        return deltaBase > 0 ? deltaValue / (double)deltaBase * 100 : null;
    }

    private static (IReadOnlyList<string> Drivers, IReadOnlyList<string> Devices, bool Available) ReadInventory()
    {
        var drivers = WmiQuery.Query("SELECT Name FROM Win32_SystemDriver WHERE State = 'Running'")
            .Select(r => WmiQuery.GetString(r, "Name"))
            .Where(n => n is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var devices = WmiQuery.Query("SELECT Name FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0")
            .Select(r => WmiQuery.GetString(r, "Name"))
            .Where(n => n is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var available = drivers.Count > 0 || devices.Count > 0;
        return (drivers, devices, available);
    }

    private sealed record ProcessorRawRow
    {
        public string Name { get; init; } = "_Total";
        public long? Interrupts { get; init; }
        public long? Dpcs { get; init; }
        public long? DpcRate { get; init; }
        public long? Privileged { get; init; }
        public long? Processor { get; init; }
        public long? Timestamp100Ns { get; init; }
    }
}