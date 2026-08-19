using System.Diagnostics;

namespace PCDiag.Core;

/// <summary>
/// Discovers and executes diagnostic checks, isolating failures and aggregating
/// results into a <see cref="ScanSummary"/>.
/// </summary>
public sealed class Scanner
{
    private readonly IReadOnlyList<IDiagnosticCheck> _checks;

    public Scanner(IEnumerable<IDiagnosticCheck> checks)
    {
        _checks = checks.ToList().AsReadOnly();
    }

    /// <summary>All registered checks.</summary>
    public IReadOnlyList<IDiagnosticCheck> Checks => _checks;

    /// <summary>
    /// Run all checks applicable in the given context.
    /// <paramref name="onCheckCompleted"/> is invoked with each finished check and its result,
    /// allowing callers to report live progress without coupling the scanner to the console.
    /// </summary>
    public async Task<ScanSummary> ScanAsync(
        DiagnosticContext context,
        Action<IDiagnosticCheck, DiagnosticResult>? onCheckCompleted = null)
    {
        var stopwatch = Stopwatch.StartNew();

        var applicable = _checks.Where(c => AppliesInMode(c, context)).ToList();
        var results = new List<DiagnosticResult>(applicable.Count);

        foreach (var check in applicable)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            DiagnosticResult result;
            if (check.RequiresAdmin && !context.IsAdministrator)
            {
                result = PermissionDeniedResult(check);
            }
            else
            {
                result = await ExecuteSafelyAsync(check, context).ConfigureAwait(false);
            }

            results.Add(result);
            onCheckCompleted?.Invoke(check, result);
        }

        stopwatch.Stop();
        return new ScanSummary(results.AsReadOnly(), stopwatch.Elapsed);
    }

    /// <summary>Run a single check by ID and return its result, or null if unknown.</summary>
    public async Task<DiagnosticResult?> RunAsync(string checkId, DiagnosticContext context)
    {
        var check = _checks.FirstOrDefault(c =>
            string.Equals(c.CheckId, checkId, StringComparison.OrdinalIgnoreCase));

        if (check is null)
            return null;

        context.CancellationToken.ThrowIfCancellationRequested();

        if (check.RequiresAdmin && !context.IsAdministrator)
            return PermissionDeniedResult(check);

        return await ExecuteSafelyAsync(check, context).ConfigureAwait(false);
    }

    private static async Task<DiagnosticResult> ExecuteSafelyAsync(IDiagnosticCheck check, DiagnosticContext context)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        linked.CancelAfter(context.DefaultTimeout);

        try
        {
            return await check.ExecuteAsync(context, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Only the per-check timeout fired; the user did not cancel the scan.
            if (context.CancellationToken.IsCancellationRequested)
                throw;

            return new DiagnosticResult
            {
                CheckId = check.CheckId,
                Name = check.Name,
                Category = check.Category,
                Severity = DiagnosticSeverity.Info,
                Status = DiagnosticStatus.Error,
                Summary = $"{check.Name} timed out after {context.DefaultTimeout.TotalSeconds:0.#}s.",
                RequiresAdmin = check.RequiresAdmin,
                Errors = new[]
                {
                    new DiagnosticError { Code = "timeout", Message = "The check did not complete within the configured timeout." }
                }
            };
        }
        catch (Exception ex)
        {
            return new DiagnosticResult
            {
                CheckId = check.CheckId,
                Name = check.Name,
                Category = check.Category,
                Severity = DiagnosticSeverity.Info,
                Status = DiagnosticStatus.Error,
                Summary = $"{check.Name} failed unexpectedly: {ex.Message}",
                RequiresAdmin = check.RequiresAdmin,
                Errors = new[]
                {
                    new DiagnosticError { Code = "unexpected-error", Message = ex.Message, Exception = ex }
                }
            };
        }
    }

    private static DiagnosticResult PermissionDeniedResult(IDiagnosticCheck check)
        => new()
        {
            CheckId = check.CheckId,
            Name = check.Name,
            Category = check.Category,
            Severity = DiagnosticSeverity.Info,
            Status = DiagnosticStatus.PermissionDenied,
            Summary = $"Administrator privileges are required to run {check.Name}.",
            RequiresAdmin = true,
            Errors = new[]
            {
                new DiagnosticError { Code = "permission-denied", Message = "Administrator privileges are required." }
            }
        };

    private static bool AppliesInMode(IDiagnosticCheck check, DiagnosticContext context)
    {
        // Quick scans skip checks that require elevation.
        if (context.Mode == ScanMode.Quick && check.RequiresAdmin)
            return false;

        return true;
    }
}