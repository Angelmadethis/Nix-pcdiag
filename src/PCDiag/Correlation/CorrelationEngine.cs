using PCDiag.Core;

namespace PCDiag.Correlation;

/// <summary>
/// Applies a set of <see cref="ICorrelationRule"/> instances to a list of
/// diagnostic results and collects all correlations that match. This is the
/// main entry point for the correlation subsystem.
/// </summary>
public sealed class CorrelationEngine
{
    private readonly IReadOnlyList<ICorrelationRule> _rules;

    public CorrelationEngine(IReadOnlyList<ICorrelationRule>? rules = null)
    {
        _rules = rules ?? DefaultRules();
    }

    /// <summary>
    /// Analyze the scan results and return all correlations found, ordered by
    /// severity (worst first), then by confidence (highest first).
    /// </summary>
    public IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results)
    {
        var findings = results
            .Where(r => r.Status == DiagnosticStatus.Finding)
            .ToList();

        if (findings.Count < 2)
            return Array.Empty<DiagnosticCorrelation>();

        var correlations = new List<DiagnosticCorrelation>();

        foreach (var rule in _rules)
        {
            correlations.AddRange(rule.Analyze(findings));
        }

        return correlations
            .OrderByDescending(c => c.Severity)
            .ThenByDescending(c => c.Confidence)
            .ToList();
    }

    /// <summary>
    /// Returns the default set of correlation rules covering network,
    /// hardware, and performance patterns.
    /// </summary>
    public static IReadOnlyList<ICorrelationRule> DefaultRules() => new ICorrelationRule[]
    {
        new Rules.NetworkInstabilityRule(),
        new Rules.DnsDegradationRule(),
        new Rules.TcpStackCorruptionRule(),
        new Rules.HardwareNetworkLinkRule(),
        new Rules.DiskMemoryPressureRule()
    };
}
