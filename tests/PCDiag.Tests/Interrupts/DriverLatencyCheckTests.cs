using PCDiag.Checks.Hardware;
using PCDiag.Core;
using PCDiag.Interrupts;

namespace PCDiag.Tests.Interrupts;

public class DriverLatencyCheckTests
{
    private readonly FakeInterruptSnapshotSource _source = new();

    private static DiagnosticContext Ctx() => new(mode: ScanMode.Standard);

    private async Task<DiagnosticResult> Run()
        => await new DriverLatencyCheck(_source).ExecuteAsync(Ctx(), CancellationToken.None);

    [Fact]
    public async Task HealthyActivity_ShouldPassWithHighConfidence()
    {
        _source.Snapshot = Int.Snapshot(
            Int.Total(interrupts: 1_000, dpcs: 200, privileged: 5, cpu: 10),
            Int.Core("0", 500, 100, 4, 8),
            Int.Core("1", 500, 100, 6, 12));

        var result = await Run();

        Assert.Equal(DiagnosticStatus.Passed, result.Status);
        Assert.Equal(DiagnosticSeverity.Healthy, result.Severity);
        Assert.Equal(0.9, result.Confidence);
        Assert.Contains("No sustained elevated", result.Summary);
    }

    [Fact]
    public async Task ElevatedInterruptActivity_ShouldBeSuspiciousFinding()
    {
        _source.Snapshot = Int.Snapshot(
            Int.Total(interrupts: 15_000, dpcs: 500, privileged: 10, cpu: 40),
            Int.Core("0", 8_000, 250, 10, 40),
            Int.Core("1", 7_000, 250, 10, 40));

        var result = await Run();

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Suspicious, result.Severity);
        Assert.Contains("Elevated interrupt or DPC activity", result.Summary);
        Assert.Contains(result.Evidence, e => e.Description == "Total Activity (_Total)");
        Assert.Contains(result.Evidence, e => e.Description == "CPU Correlation");
        Assert.Equal(0.5, result.Confidence, 2);
    }

    [Fact]
    public async Task HighActivity_ShouldBeWarningFindingWithHigherConfidence()
    {
        _source.Snapshot = Int.Snapshot(
            Int.Total(interrupts: 30_000, dpcs: 9_000, privileged: 45, cpu: 60),
            Int.Core("0", 15_000, 4_000, 45, 60),
            Int.Core("1", 15_000, 5_000, 45, 60));

        var result = await Run();

        Assert.Equal(DiagnosticStatus.Finding, result.Status);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Equal(0.7, result.Confidence, 2);
    }

    [Fact]
    public async Task UnavailableCounters_ShouldBeUnavailableNotHealthy()
    {
        _source.Snapshot = new InterruptSnapshot
        {
            Total = null,
            Cores = Array.Empty<InterruptCoreSample>(),
            CountersAvailable = false
        };

        var result = await Run();

        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
        Assert.Equal(DiagnosticSeverity.Info, result.Severity);
        Assert.Contains("could not be read", result.Summary);
    }

    [Fact]
    public async Task Evidence_ShouldStateMeasurementHonesty()
    {
        _source.Snapshot = Int.Snapshot(
            Int.Total(interrupts: 1_000, dpcs: 200, privileged: 5, cpu: 10),
            Int.Core("0", 500, 100, 4, 8));

        var result = await Run();

        var honesty = result.Evidence.Single(e => e.Description == "Measurement Honesty");
        Assert.Contains("activity RATES", honesty.Value);
        Assert.Contains("not", honesty.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ETW", honesty.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evidence_ShouldShowContextOnlyInventory()
    {
        _source.Snapshot = new InterruptSnapshot
        {
            Total = Int.Total(interrupts: 1_000, dpcs: 200),
            Cores = new[] { Int.Core("0", 500, 100) },
            CountersAvailable = true,
            TopologyAvailable = true,
            InventoryAvailable = true,
            LoadedDrivers = new[] { "Netwtw", "ndis", "nvlddmkm" },
            Devices = new[] { "NVIDIA GeForce", "Intel Wi-Fi" }
        };

        var result = await Run();

        var drivers = result.Evidence.Single(e => e.Description == "Loaded Drivers (context)");
        Assert.Contains("Context only", drivers.Value);
        Assert.Contains("3 running driver(s)", drivers.Value);
        var devices = result.Evidence.Single(e => e.Description == "Devices Present (context)");
        Assert.Contains("NVIDIA GeForce", devices.Value);
        Assert.Contains("Intel Wi-Fi", devices.Value);
    }

    [Fact]
    public async Task Recommendations_ShouldNeverSuggestUninstalling()
    {
        _source.Snapshot = Int.Snapshot(
            Int.Total(interrupts: 30_000, dpcs: 9_000, privileged: 45, cpu: 60),
            Int.Core("0", 15_000, 4_000, 45, 60),
            Int.Core("1", 15_000, 5_000, 45, 60));

        var result = await Run();

        Assert.NotEmpty(result.Recommendations);
        Assert.DoesNotContain(result.Recommendations, r => r.Text.TrimStart().StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Recommendations, r => r.Text.Contains("LatencyMon", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Recommendations, r => r.RequiresAdmin);
    }

    [Fact]
    public async Task Limitations_ShouldStateLatencyScope()
    {
        _source.Snapshot = Int.Snapshot(Int.Total(interrupts: 1_000, dpcs: 200));

        var result = await Run();

        Assert.Contains(result.Limitations, l => l.Contains("does NOT measure true per-DPC latency"));
        Assert.Contains(result.Limitations, l => l.Contains("uninstallation"));
    }
}