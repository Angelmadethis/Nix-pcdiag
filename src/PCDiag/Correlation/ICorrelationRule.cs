using PCDiag.Core;

namespace PCDiag.Correlation;

/// <summary>
/// A rule that analyzes a set of diagnostic results and produces correlations
/// when a recognizable pattern of related findings is detected. Each rule
/// encapsulates one specific pattern (e.g. "network instability" = gateway +
/// packet loss + TCP issues all present).
/// </summary>
public interface ICorrelationRule
{
    /// <summary>
    /// Analyze the given results and return zero or more correlations.
    /// Return an empty collection when the pattern does not match or when
    /// evidence is conflicting (e.g. one finding is healthy while another
    /// in the same pattern is a finding).
    /// </summary>
    IReadOnlyList<DiagnosticCorrelation> Analyze(IReadOnlyList<DiagnosticResult> results);
}
