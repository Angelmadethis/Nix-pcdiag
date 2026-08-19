namespace PCDiag.Core;

/// <summary>
/// A structured error attached to a diagnostic result.
/// </summary>
public sealed record DiagnosticError
{
    /// <summary>A machine-readable error code (e.g., "timeout", "permission-denied").</summary>
    public required string Code { get; init; }

    /// <summary>A human-readable error message.</summary>
    public required string Message { get; init; }

    /// <summary>The underlying exception, if one was captured.</summary>
    public Exception? Exception { get; init; }
}