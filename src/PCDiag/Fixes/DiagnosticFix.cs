namespace PCDiag.Fixes;

/// <summary>
/// A single fixable remediation that a diagnostic can offer. A check that wants to
/// propose fixes implements <see cref="PCDiag.Core.IFixableCheck"/> and returns the
/// fixes relevant to a given finding. Fixes are always applied only after explicit
/// user confirmation; nothing here is ever applied automatically.
/// </summary>
public abstract class DiagnosticFix
{
    /// <summary>Stable identifier for this fix (e.g. "dns-flush-cache").</summary>
    public abstract string Id { get; }

    /// <summary>What the fix does, e.g. "Flush the Windows DNS resolver cache".</summary>
    public abstract string Title { get; }

    /// <summary>What problem the fix addresses, derived from the detected issue.</summary>
    public abstract string Problem { get; }

    /// <summary>Expected effect, including anything the fix will <em>not</em> change.</summary>
    public abstract string Effect { get; }

    /// <summary>How invasive or risky the change is.</summary>
    public abstract FixRisk Risk { get; }

    /// <summary>Whether administrator privileges are required to apply this fix.</summary>
    public virtual bool RequiresAdmin => false;

    /// <summary>
    /// Apply the fix. Must be idempotent and must not touch anything outside the
    /// described effect. Returns the outcome of the apply step.
    /// </summary>
    public abstract Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional targeted verification for this fix. Return null to verify by
    /// re-running the owning diagnostic after applying.
    /// </summary>
    public virtual Task<bool?> VerifyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<bool?>(null);
}