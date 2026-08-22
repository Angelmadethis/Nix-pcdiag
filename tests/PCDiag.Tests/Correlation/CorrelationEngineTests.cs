using PCDiag.Core;
using PCDiag.Correlation;

namespace PCDiag.Tests.Correlation;

public class CorrelationEngineTests
{
    private static DiagnosticResult Finding(
        string checkId,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        double confidence = 0.8,
        params DiagnosticEvidence[] evidence)
        => new()
        {
            CheckId = checkId,
            Name = $"Check {checkId}",
            Category = DiagnosticCategory.Network,
            Severity = severity,
            Status = DiagnosticStatus.Finding,
            Summary = $"Finding from {checkId}.",
            Confidence = confidence,
            Evidence = evidence
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
    public void Analyze_EmptyResults_ShouldReturnEmpty()
    {
        var engine = new CorrelationEngine();

        var correlations = engine.Analyze(Array.Empty<DiagnosticResult>());

        Assert.Empty(correlations);
    }

    [Fact]
    public void Analyze_SingleFinding_ShouldReturnEmpty()
    {
        var engine = new CorrelationEngine();
        var results = new[] { Finding("NET-GWY-001") };

        var correlations = engine.Analyze(results);

        Assert.Empty(correlations);
    }

    [Fact]
    public void Analyze_AllHealthy_ShouldReturnEmpty()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Healthy("NET-GWY-001"),
            Healthy("NET-LOSS-001"),
            Healthy("NET-TCP-001")
        };

        var correlations = engine.Analyze(results);

        Assert.Empty(correlations);
    }

    [Fact]
    public void Analyze_NetworkInstabilityPattern_ShouldCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-GWY-001", DiagnosticSeverity.Critical, 0.9),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning, 0.8),
            Finding("NET-TCP-001", DiagnosticSeverity.Suspicious, 0.7)
        };

        var correlations = engine.Analyze(results);

        Assert.Contains(correlations, c => c.Id == "CORR-NET-001");
        var corr = correlations.First(c => c.Id == "CORR-NET-001");
        Assert.Equal("Network Instability", corr.Title);
        Assert.Equal(DiagnosticSeverity.Critical, corr.Severity);
        Assert.Contains("NET-GWY-001", corr.RelatedCheckIds);
        Assert.Contains("NET-LOSS-001", corr.RelatedCheckIds);
        Assert.Contains("NET-TCP-001", corr.RelatedCheckIds);
        Assert.NotEmpty(corr.Recommendations);
        Assert.NotEmpty(corr.RootCauses);
    }

    [Fact]
    public void Analyze_DnsDegradationPattern_ShouldCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-DNS-001", DiagnosticSeverity.Warning, 0.85),
            Finding("NET-GWY-001", DiagnosticSeverity.Suspicious, 0.75)
        };

        var correlations = engine.Analyze(results);

        var corr = Assert.Single(correlations);
        Assert.Equal("CORR-NET-002", corr.Id);
        Assert.Equal("DNS Degradation with Gateway Issues", corr.Title);
    }

    [Fact]
    public void Analyze_TcpStackCorruptionPattern_ShouldCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-TCP-001", DiagnosticSeverity.Warning, 0.8),
            Finding("NET-LOSS-001", DiagnosticSeverity.Suspicious, 0.7)
        };

        var correlations = engine.Analyze(results);

        var corr = Assert.Single(correlations);
        Assert.Equal("CORR-NET-003", corr.Id);
        Assert.Equal("TCP Stack Corruption", corr.Title);
        Assert.Contains("NET-TCP-001", corr.RelatedCheckIds);
        Assert.Contains("NET-LOSS-001", corr.RelatedCheckIds);
    }

    [Fact]
    public void Analyze_HardwareNetworkLinkPattern_ShouldCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("HW-WHEA-001", DiagnosticSeverity.Warning, 0.8),
            Finding("NET-GWY-001", DiagnosticSeverity.Critical, 0.9)
        };

        var correlations = engine.Analyze(results);

        var corr = Assert.Single(correlations);
        Assert.Equal("CORR-HW-NET-001", corr.Id);
        Assert.Equal("Hardware-Network Link", corr.Title);
    }

    [Fact]
    public void Analyze_DiskMemoryPressurePattern_ShouldCorrelate()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("PERF-DISK-001", DiagnosticSeverity.Warning, 0.85),
            Finding("PERF-MEM-001", DiagnosticSeverity.Critical, 0.9)
        };

        var correlations = engine.Analyze(results);

        var corr = Assert.Single(correlations);
        Assert.Equal("CORR-SYS-001", corr.Id);
        Assert.Equal("System Resource Pressure", corr.Title);
    }

    [Fact]
    public void Analyze_MultiplePatterns_ShouldReturnAll()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-GWY-001", DiagnosticSeverity.Critical, 0.9),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning, 0.8),
            Finding("NET-TCP-001", DiagnosticSeverity.Suspicious, 0.7),
            Finding("NET-DNS-001", DiagnosticSeverity.Warning, 0.85),
            Finding("PERF-DISK-001", DiagnosticSeverity.Warning, 0.85),
            Finding("PERF-MEM-001", DiagnosticSeverity.Critical, 0.9)
        };

        var correlations = engine.Analyze(results);

        Assert.True(correlations.Count >= 3);
        Assert.Contains(correlations, c => c.Id == "CORR-NET-001");
        Assert.Contains(correlations, c => c.Id == "CORR-NET-002");
        Assert.Contains(correlations, c => c.Id == "CORR-SYS-001");
    }

    [Fact]
    public void Analyze_ShouldOrderBySeverityDescending()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("PERF-DISK-001", DiagnosticSeverity.Warning, 0.85),
            Finding("PERF-MEM-001", DiagnosticSeverity.Critical, 0.9),
            Finding("NET-GWY-001", DiagnosticSeverity.Critical, 0.9),
            Finding("NET-LOSS-001", DiagnosticSeverity.Critical, 0.9),
            Finding("NET-TCP-001", DiagnosticSeverity.Critical, 0.9)
        };

        var correlations = engine.Analyze(results);

        Assert.True(correlations.Count >= 2);
        Assert.True(correlations[0].Severity >= correlations[1].Severity);
    }

    [Fact]
    public void Analyze_ConfidenceShouldBeReduced()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-GWY-001", DiagnosticSeverity.Warning, 1.0),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning, 1.0),
            Finding("NET-TCP-001", DiagnosticSeverity.Warning, 1.0)
        };

        var correlations = engine.Analyze(results);

        Assert.Contains(correlations, c => c.Id == "CORR-NET-001");
        var corr = correlations.First(c => c.Id == "CORR-NET-001");
        Assert.True(corr.Confidence < 1.0);
        Assert.True(corr.Confidence > 0.5);
    }

    [Fact]
    public void Analyze_MinConfidenceShouldDetermineCorrelationConfidence()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Finding("NET-GWY-001", DiagnosticSeverity.Warning, 0.9),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning, 0.6),
            Finding("NET-TCP-001", DiagnosticSeverity.Warning, 0.8)
        };

        var correlations = engine.Analyze(results);

        Assert.Contains(correlations, c => c.Id == "CORR-NET-001");
        var corr = correlations.First(c => c.Id == "CORR-NET-001");
        // min confidence is 0.6, reduced by 0.85 = 0.51
        Assert.InRange(corr.Confidence, 0.50, 0.55);
    }

    [Fact]
    public void Analyze_OnlyFindingResults_ShouldBeConsidered()
    {
        var engine = new CorrelationEngine();
        var results = new[]
        {
            Healthy("NET-GWY-001"),
            Finding("NET-LOSS-001", DiagnosticSeverity.Warning, 0.8),
            Finding("NET-TCP-001", DiagnosticSeverity.Warning, 0.7)
        };

        var correlations = engine.Analyze(results);

        // Gateway is healthy, so NetworkInstability should not trigger
        Assert.DoesNotContain(correlations, c => c.Id == "CORR-NET-001");
    }
}
