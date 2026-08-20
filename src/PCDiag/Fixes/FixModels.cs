namespace PCDiag.Fixes;

/// <summary>How invasive a fix is. Displayed to the user before applying.</summary>
public enum FixRisk
{
    /// <summary>Reversible, no lasting state change (e.g. flushing a cache).</summary>
    Low,

    /// <summary>Changes a configuration value or system state.</summary>
    Medium,

    /// <summary>Can cause downtime or requires a restart.</summary>
    High
}

/// <summary>Result of the apply step of a fix.</summary>
public enum FixApplyOutcome
{
    /// <summary>The fix was applied successfully.</summary>
    Applied,

    /// <summary>The fix could not be applied.</summary>
    Failed,

    /// <summary>The fix was not attempted (e.g. requires admin and the process is not elevated).</summary>
    NotApplicable
}

/// <summary>The result of applying a single fix.</summary>
public sealed record FixApplyResult
{
    /// <summary>Whether the apply step succeeded.</summary>
    public required FixApplyOutcome Outcome { get; init; }

    /// <summary>Human-readable message shown to the user (e.g. "DNS cache was successfully flushed.").</summary>
    public required string Message { get; init; }

    /// <summary>Optional detail when the apply failed.</summary>
    public string? ErrorDetail { get; init; }
}

/// <summary>The result of a full apply-then-verify fix execution.</summary>
public sealed record FixExecutionResult
{
    /// <summary>True when the apply step succeeded.</summary>
    public required bool Applied { get; init; }

    /// <summary>True when verification showed the issue cleared.</summary>
    public required bool Resolved { get; init; }

    /// <summary>Human-readable summary of the outcome.</summary>
    public required string Message { get; init; }

    /// <summary>Optional detail when the fix could not be applied.</summary>
    public string? ErrorDetail { get; init; }

    /// <summary>Result of the re-run used for verification, when one was performed.</summary>
    public PCDiag.Core.DiagnosticResult? RecheckResult { get; init; }
}