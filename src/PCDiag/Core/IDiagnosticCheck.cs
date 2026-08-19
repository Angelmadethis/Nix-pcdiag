namespace PCDiag.Core;

/// <summary>
/// The contract that every diagnostic check must implement.
/// Each check is an independent, self-contained unit of analysis.
/// </summary>
public interface IDiagnosticCheck
{
    /// <summary>Unique identifier for this check (e.g., "WIN-ENV-001").</summary>
    string CheckId { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Category this check belongs to.</summary>
    DiagnosticCategory Category { get; }

    /// <summary>Brief description of what this check does.</summary>
    string Description { get; }

    /// <summary>Whether administrator privileges are needed.</summary>
    bool RequiresAdmin { get; }

    /// <summary>
    /// Run the check synchronously and return the result.
    /// Checks must never modify the system.
    /// </summary>
    DiagnosticResult Execute(DiagnosticContext context);

    /// <summary>
    /// Run the check asynchronously and return the result.
    /// Checks must never modify the system.
    /// </summary>
    Task<DiagnosticResult> ExecuteAsync(DiagnosticContext context, CancellationToken cancellationToken = default);
}