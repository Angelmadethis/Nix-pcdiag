namespace PCDiag.Core;

/// <summary>
/// Severity levels for diagnostic findings, ordered from least to most severe.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>System is operating normally for this check.</summary>
    Healthy = 0,

    /// <summary>Informational finding. Not a problem, but worth noting.</summary>
    Info = 1,

    /// <summary>Something unusual detected, but not necessarily harmful.</summary>
    Suspicious = 2,

    /// <summary>Problem detected that may impact performance or stability.</summary>
    Warning = 3,

    /// <summary>Serious problem detected that likely impacts the system.</summary>
    Critical = 4
}