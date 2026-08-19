namespace PCDiag.Core;

/// <summary>
/// A recommended next step for the user.
/// </summary>
public sealed record DiagnosticRecommendation
{
    /// <summary>The recommendation text.</summary>
    public required string Text { get; init; }

    /// <summary>Whether this can be automated (for a future pcdiag fix command).</summary>
    public bool Automatable { get; init; }

    /// <summary>Whether admin privileges are needed to follow this recommendation.</summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>Priority ordering: lower = more urgent.</summary>
    public int Priority { get; init; }
}