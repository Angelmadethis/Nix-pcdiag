namespace PCDiag.Core;

/// <summary>
/// A single piece of evidence collected by a diagnostic check.
/// </summary>
public sealed record DiagnosticEvidence
{
    /// <summary>What this evidence item represents.</summary>
    public required string Description { get; init; }

    /// <summary>The measured or observed value.</summary>
    public required string Value { get; init; }

    /// <summary>The expected or reference value (if applicable).</summary>
    public string? ExpectedValue { get; init; }

    /// <summary>The source of this evidence (command, API, file, etc.).</summary>
    public string? Source { get; init; }
}