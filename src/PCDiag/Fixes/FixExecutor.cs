using PCDiag.Core;

namespace PCDiag.Fixes;

/// <summary>
/// Reusable orchestrator for applying a fix and verifying the result. The flow is:
/// <list type="number">
/// <item>Guard admin requirement - a fix that needs elevation is never attempted when the process is not elevated.</item>
/// <item>Apply the fix.</item>
/// <item>Verify - either a fix-specific check, or by re-running the owning diagnostic.</item>
/// </list>
/// The executor is deliberately UI-agnostic so both the TUI and any future CLI can reuse it.
/// </summary>
public sealed class FixExecutor
{
    /// <summary>
    /// Apply <paramref name="fix"/> for <paramref name="check"/> and verify the result by
    /// re-running the diagnostic (unless the fix supplies its own verification).
    /// </summary>
    public async Task<FixExecutionResult> ExecuteAsync(
        IDiagnosticCheck check,
        DiagnosticFix fix,
        DiagnosticContext context,
        DiagnosticResult original,
        CancellationToken cancellationToken = default)
    {
        if (fix.RequiresAdmin && !context.IsAdministrator)
        {
            return new FixExecutionResult
            {
                Applied = false,
                Resolved = false,
                Message = $"{fix.Title} requires administrator privileges. Run PCDiag as administrator and try again."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var apply = await fix.ApplyAsync(cancellationToken).ConfigureAwait(false);
        if (apply.Outcome != FixApplyOutcome.Applied)
        {
            return new FixExecutionResult
            {
                Applied = false,
                Resolved = false,
                Message = apply.Message,
                ErrorDetail = apply.ErrorDetail
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var verified = await fix.VerifyAsync(cancellationToken).ConfigureAwait(false);

        DiagnosticResult? recheck = null;
        if (verified is bool targeted)
        {
            return new FixExecutionResult
            {
                Applied = true,
                Resolved = targeted,
                Message = targeted
                    ? $"{fix.Title} resolved the issue."
                    : $"{fix.Title} was applied but the issue persists."
            };
        }

        recheck = await check.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        var resolved = IsResolved(recheck, original);

        return new FixExecutionResult
        {
            Applied = true,
            Resolved = resolved,
            Message = resolved
                ? $"{original.Name} no longer detects the issue."
                : $"{original.Name} still detects the issue after applying the fix.",
            RecheckResult = recheck
        };
    }

    /// <summary>True when the re-run result is countable and shows the issue cleared.</summary>
    private static bool IsResolved(DiagnosticResult recheck, DiagnosticResult original)
    {
        return recheck.Status != DiagnosticStatus.Error
               && recheck.Status != DiagnosticStatus.Skipped
               && recheck.Status != DiagnosticStatus.Unavailable
               && recheck.Status != DiagnosticStatus.PermissionDenied
               && (recheck.Status == DiagnosticStatus.Passed || recheck.Severity < original.Severity);
    }
}