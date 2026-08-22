using PCDiag.Core;

namespace PCDiag.Correlation;

/// <summary>
/// A relationship between multiple diagnostic findings that share a common root cause
/// or represent a coherent problem pattern. Correlations are produced by the
/// <see cref="CorrelationEngine"/> after a scan completes and are displayed alongside
/// individual results to help the user understand the bigger picture.
/// </summary>
public sealed record DiagnosticCorrelation
{
    /// <summary>Unique identifier, e.g. "CORR-NET-001".</summary>
    public required string Id { get; init; }

    /// <summary>Short human-readable title, e.g. "Network Instability".</summary>
    public required string Title { get; init; }

    /// <summary>One-line summary of the correlation.</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed explanation of why the findings are related.</summary>
    public required string Detail { get; init; }

    /// <summary>Confidence that this correlation is valid (0.0–1.0).</summary>
    public required double Confidence { get; init; }

    /// <summary>Severity derived from the worst participating finding.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Check IDs of all findings involved in this correlation.</summary>
    public required IReadOnlyList<string> RelatedCheckIds { get; init; }

    /// <summary>Consolidated evidence from all participating findings.</summary>
    public required IReadOnlyList<DiagnosticEvidence> ConsolidatedEvidence { get; init; }

    /// <summary>Unified recommendations that address the root cause.</summary>
    public required IReadOnlyList<DiagnosticRecommendation> Recommendations { get; init; }

    /// <summary>Likely root causes that explain all findings together.</summary>
    public required IReadOnlyList<string> RootCauses { get; init; }
}
