namespace PCDiag.Core;

/// <summary>
/// The structured result returned by every diagnostic check.
/// This is the primary data contract of the scanner.
/// </summary>
public sealed record DiagnosticResult
{
    /// <summary>Unique identifier for this check (e.g., "WIN-ENV-001").</summary>
    public required string CheckId { get; init; }

    /// <summary>Human-readable name of the check.</summary>
    public required string Name { get; init; }

    /// <summary>The category this check belongs to.</summary>
    public required DiagnosticCategory Category { get; init; }

    /// <summary>Severity of the finding.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Execution status of the check.</summary>
    public required DiagnosticStatus Status { get; init; }

    /// <summary>Brief one-line summary of the finding.</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed explanation of what was detected and why it matters.</summary>
    public string? Detail { get; init; }

    /// <summary>Possible causes of the finding, when the check can propose any.</summary>
    public IReadOnlyList<string> PossibleCauses { get; init; } = Array.Empty<string>();

    /// <summary>Known limitations or blind spots of this check.</summary>
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    /// <summary>All evidence collected during this check.</summary>
    public IReadOnlyList<DiagnosticEvidence> Evidence { get; init; } = Array.Empty<DiagnosticEvidence>();

    /// <summary>Confidence in the diagnosis (0.0 to 1.0).</summary>
    public double Confidence { get; init; }

    /// <summary>Recommended next steps.</summary>
    public IReadOnlyList<DiagnosticRecommendation> Recommendations { get; init; } = Array.Empty<DiagnosticRecommendation>();

    /// <summary>Any structured errors encountered during the check.</summary>
    public IReadOnlyList<DiagnosticError> Errors { get; init; } = Array.Empty<DiagnosticError>();

    /// <summary>Whether administrator privileges are required for this check.</summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>How long the check took to execute.</summary>
    public TimeSpan Duration { get; init; }
}