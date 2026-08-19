using System.Diagnostics;

namespace PCDiag.Core;

/// <summary>
/// Base class for diagnostic checks.
///
/// Sync checks override <see cref="Run"/>. Async checks override <see cref="RunAsync"/>.
/// <see cref="ExecuteAsync"/> measures execution time and converts exceptions into
/// structured error results so a single failing check never breaks a scan.
/// </summary>
public abstract class DiagnosticCheck : IDiagnosticCheck
{
    public abstract string CheckId { get; }
    public abstract string Name { get; }
    public abstract DiagnosticCategory Category { get; }
    public abstract string Description { get; }
    public virtual bool RequiresAdmin => false;

    /// <summary>Runs the check synchronously.</summary>
    public DiagnosticResult Execute(DiagnosticContext context)
        => Run(context);

    /// <summary>
    /// Runs the check asynchronously, measuring duration and isolating exceptions
    /// into structured error results.
    /// </summary>
    public async Task<DiagnosticResult> ExecuteAsync(DiagnosticContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await RunAsync(context, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return result with { Duration = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            return PermissionDenied($"Insufficient permissions to run {Name}.", ex);
        }
        catch (NotSupportedException ex)
        {
            return Unavailable($"{Name} is not available on this system.", ex);
        }
        catch (DiagnosticCheckException ex)
        {
            return Error(ex.Code, ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Error("unexpected-error", $"{Name} failed unexpectedly: {ex.Message}", ex);
        }
    }

    /// <summary>Synchronous implementation. Override for sync checks.</summary>
    protected virtual DiagnosticResult Run(DiagnosticContext context)
        => throw new NotSupportedException($"{GetType().Name} does not implement synchronous execution.");

    /// <summary>Asynchronous implementation. Override for async checks.</summary>
    protected virtual Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
        => Task.FromResult(Run(context));

    /// <summary>Build a fully-populated result for this check.</summary>
    protected DiagnosticResult BuildResult(
        DiagnosticSeverity severity,
        DiagnosticStatus status,
        string summary,
        string? detail = null,
        IReadOnlyList<DiagnosticEvidence>? evidence = null,
        IReadOnlyList<DiagnosticRecommendation>? recommendations = null,
        IReadOnlyList<string>? possibleCauses = null,
        IReadOnlyList<string>? limitations = null,
        IReadOnlyList<DiagnosticError>? errors = null,
        double confidence = 1.0)
    {
        return new DiagnosticResult
        {
            CheckId = CheckId,
            Name = Name,
            Category = Category,
            Severity = severity,
            Status = status,
            Summary = summary,
            Detail = detail,
            Evidence = evidence ?? Array.Empty<DiagnosticEvidence>(),
            Recommendations = recommendations ?? Array.Empty<DiagnosticRecommendation>(),
            PossibleCauses = possibleCauses ?? Array.Empty<string>(),
            Limitations = limitations ?? Array.Empty<string>(),
            Errors = errors ?? Array.Empty<DiagnosticError>(),
            Confidence = confidence,
            RequiresAdmin = RequiresAdmin
        };
    }

    /// <summary>A result indicating the check could not run because of missing permissions.</summary>
    protected DiagnosticResult PermissionDenied(string summary, Exception? exception = null)
        => BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.PermissionDenied,
            summary,
            errors: new[]
            {
                new DiagnosticError { Code = "permission-denied", Message = summary, Exception = exception }
            });

    /// <summary>A result indicating the check could not run because a capability is missing.</summary>
    protected DiagnosticResult Unavailable(string summary, Exception? exception = null)
        => BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            summary,
            errors: new[]
            {
                new DiagnosticError { Code = "unavailable", Message = summary, Exception = exception }
            });

    /// <summary>A result indicating the check failed with a structured error.</summary>
    protected DiagnosticResult Error(string code, string summary, Exception? exception = null, string? detail = null)
        => BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Error,
            summary,
            detail,
            errors: new[]
            {
                new DiagnosticError { Code = code, Message = summary, Exception = exception }
            });
}