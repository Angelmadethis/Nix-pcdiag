namespace PCDiag.Core;

/// <summary>
/// Execution status of a diagnostic check.
/// </summary>
public enum DiagnosticStatus
{
    /// <summary>Check completed and found no issues.</summary>
    Passed,

    /// <summary>Check completed and found something worth reporting.</summary>
    Finding,

    /// <summary>Check could not complete due to an error.</summary>
    Error,

    /// <summary>Check was skipped (e.g., not applicable in this scan mode).</summary>
    Skipped,

    /// <summary>Check could not run because a required capability is missing.</summary>
    Unavailable,

    /// <summary>Check could not run because the required permissions were not available.</summary>
    PermissionDenied
}