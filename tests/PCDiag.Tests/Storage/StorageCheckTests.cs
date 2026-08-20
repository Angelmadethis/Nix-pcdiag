using PCDiag.Checks.Performance;
using PCDiag.Core;
using PCDiag.Storage;
using PCDiag.Tests.Events;

namespace PCDiag.Tests.Storage;

public class StorageCheckTests
{
    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private static Task<PCDiag.Core.DiagnosticResult> Run(FakeStorageInfoSource storage, FakeEventLogSource? events = null)
        => new StorageCheck(storage: storage, eventSource: events).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task HealthyVolumesAndDisk_ShouldPassHealthy()
    {
        storage.Snapshot = Sto.Snapshot(
            volumes: new[] { Sto.Volume("C:", 0.30) },
            disks: new[] { Sto.Disk(Sto.HealthyHealth()) });

        var result = await Run(storage);

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description.StartsWith("Volume C:"));
        Assert.Contains(result.Limitations, l => l.Contains("no destructive tests"));
    }

    [Fact]
    public async Task LowFreeSpace_ShouldBeSuspiciousFindingWithEvidence()
    {
        storage.Snapshot = Sto.Snapshot(volumes: new[] { Sto.Volume("C:", 0.12) });

        var result = await Run(storage);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description.StartsWith("Volume C:"));
        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task SmartUnavailable_ShouldNotClaimDrivePerfect()
    {
        storage.Snapshot = Sto.Snapshot(
            volumes: new[] { Sto.Volume("C:", 0.30) },
            disks: new[] { Sto.Disk(Sto.UnknownNoReliability()) });

        var result = await Run(storage);

        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        var diskEvidence = result.Evidence.Single(e => e.Description.StartsWith("Disk "));
        Assert.Contains("not independently verified", diskEvidence.Value);
    }

    [Fact]
    public async Task UnhealthyDisk_ShouldBeWarningFinding()
    {
        storage.Snapshot = Sto.Snapshot(
            volumes: new[] { Sto.Volume("C:", 0.30) },
            disks: new[]
            {
                Sto.Disk(new StorageHealth { StackQueried = true, StackState = StorageHealthState.Unhealthy, HasReliabilityCounters = false })
            });

        var result = await Run(storage);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description.StartsWith("Disk ") && e.Value.Contains("Unhealthy"));
    }

    [Fact]
    public async Task DiskErrorEvents_ShouldRaiseSeverityAboveStorageState()
    {
        storage.Snapshot = Sto.Snapshot(volumes: new[] { Sto.Volume("C:", 0.30) });
        var events = new FakeEventLogSource();
        events.Records.Add(Ev.New("disk", 11));

        var result = await Run(storage, events);

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description.Contains("Disk Errors", StringComparison.OrdinalIgnoreCase)
                                              || e.Description.Contains("Disk"));
    }

    [Fact]
    public async Task NothingReadable_ShouldBeUnavailable()
    {
        storage.Snapshot = Sto.Snapshot(volumesAvailable: false, disksAvailable: false, namespaceAvailable: false);

        var result = await Run(storage);

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task IdleLatency_ShouldReportIdleNotSlow()
    {
        storage.Snapshot = Sto.Snapshot(
            volumes: new[] { Sto.Volume("C:", 0.30) },
            latency: new[] { Sto.Latency("0 C:") });

        var result = await Run(storage);

        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Contains(result.Evidence, e => e.Description == "Disk Latency (0 C:)" && e.Value.Contains("idle"));
    }

    private readonly FakeStorageInfoSource storage = new();
}