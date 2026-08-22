using PCDiag.Core;
using PCDiag.Correlation;

namespace PCDiag.Tests.Correlation;

/// <summary>
/// Tests that correlations are suppressed when evidence conflicts —
/// e.g. one finding in a pattern is healthy, indicating the issues
/// are independent rather than related.
/// </summary>
public class CorrelationConflictTests
{
    private static DiagnosticResult Finding(
        string checkId,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        double confidence = 0.8)
        => new()
        {
            CheckId = checkId,
            Name = $"Check {checkId}",
            Category = DiagnosticCategory.Network,
            Severity = severity,
            Status = DiagnosticStatus.Finding,
            Summary = $"Finding from {checkId}.",
            Confidence = confidence
        };

    private static DiagnosticResult Healthy(string checkId)
        => new()
        {
            CheckId = checkId,
            Name = $"Check {checkId}",
            Category = DiagnosticCategory.Network,
            Severity = DiagnosticSeverity.Healthy,
            Status = DiagnosticStatus.Passed,
            Summary = "All clear.",
            Confidence = 0.9
        };

    [Fact]
    public void NetworkInstability_GatewayHealthy_ShouldNotCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Healthy("NET-GWY-001"),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning),
            Finding("NET-TCP-001", DiagnosticSeverity.Warning)
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-001");
    }

    [Fact]
    public void NetworkInstability_PacketLossHealthy_ShouldNotCorrelate()
    {
        // NetworkInstability requires all three checks to be findings.
        // When packet loss is healthy, the pattern does not match —
        // healthy packet loss + unhealthy gateway suggests an upstream issue,
        // not local network instability.
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-GWY-001", DiagnosticSeverity.Critical),
            Healthy("NET-LOSS-001"),
            Finding("NET-TCP-001", DiagnosticSeverity.Warning)
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-001");
    }

    [Fact]
    public void DnsDegradation_GatewayHealthy_ShouldNotCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-DNS-001", DiagnosticSeverity.Warning),
            Healthy("NET-GWY-001")
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-002");
    }

    [Fact]
    public void DnsDegradation_DnsHealthy_ShouldNotCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Healthy("NET-DNS-001"),
            Finding("NET-GWY-001", DiagnosticSeverity.Warning)
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-002");
    }

    [Fact]
    public void TcpStackCorruption_BothNetworkHealthy_ShouldNotCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-TCP-001", DiagnosticSeverity.Warning),
            Healthy("NET-GWY-001"),
            Healthy("NET-LOSS-001")
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-003");
    }

    [Fact]
    public void HardwareNetworkLink_BothHardwareHealthy_ShouldNotCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Healthy("HW-WHEA-001"),
            Healthy("HW-DRV-001"),
            Finding("NET-GWY-001", DiagnosticSeverity.Critical)
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-HW-NET-001");
    }

    [Fact]
    public void DiskMemoryPressure_EitherHealthy_ShouldNotCorrelate()
    {
        var engine = new CorrelationEngine();

        // Disk healthy, memory warning
        var results1 = new[]
        {
            Healthy("PERF-DISK-001"),
            Finding("PERF-MEM-001", DiagnosticSeverity.Warning)
        };
        Assert.Empty(engine.Analyze(results1));

        // Disk warning, memory healthy
        var results2 = new[]
        {
            Finding("PERF-DISK-001", DiagnosticSeverity.Warning),
            Healthy("PERF-MEM-001")
        };
        Assert.Empty(engine.Analyze(results2));
    }

    [Fact]
    public void DiskMemoryPressure_Suspicious_ShouldNotCorrelate()
    {
        // Only warning+ triggers this rule
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("PERF-DISK-001", DiagnosticSeverity.Suspicious),
            Finding("PERF-MEM-001", DiagnosticSeverity.Suspicious)
        };

        var correlations = engine.Analyze(results);

        Assert.DoesNotContain(correlations, c => c.Id == "CORR-SYS-001");
    }

    [Fact]
    public void MixedConflict_SomePatternsMatch_SomeDont()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            // Network instability matches (all three findings)
            Finding("NET-GWY-001", DiagnosticSeverity.Critical),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning),
            Finding("NET-TCP-001", DiagnosticSeverity.Warning),
            // DNS degradation does NOT match (gateway is finding, DNS is healthy)
            Healthy("NET-DNS-001"),
            // Disk-memory does NOT match (disk is healthy)
            Healthy("PERF-DISK-001"),
            Finding("PERF-MEM-001", DiagnosticSeverity.Warning)
        };

        var correlations = engine.Analyze(results);

        Assert.Contains(correlations, c => c.Id == "CORR-NET-001");
        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-002");
        Assert.DoesNotContain(correlations, c => c.Id == "CORR-SYS-001");
    }
}
