using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Restores TCP Receive Window Auto-Tuning to the Windows default by running
/// <c>netsh interface tcp set global autotuninglevel=normal</c>. Reversible; the same
/// command can set any other level. No restart is required. The command runner is
/// injectable so tests can simulate success and failure.
/// </summary>
public sealed class AutotuningRestoreFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _run;

    public AutotuningRestoreFix(string problem, Func<CancellationToken, Task<CommandResult>>? run = null)
    {
        Problem = problem;
        _run = run ?? (token => CommandRunner.RunAsync("netsh", "interface tcp set global autotuninglevel=normal", cancellationToken: token));
    }

    public override string Id => "tcp-autotuning-restore";
    public override string Title => "Restore TCP Receive Window Auto-Tuning to Normal";
    public override string Problem { get; }
    public override string Effect =>
        "This restores Receive Window Auto-Tuning to the Windows default level (Normal). " +
        "The change applies immediately and no restart is required; it only affects TCP " +
        "receive-window tuning, nothing else.";
    public override FixRisk Risk => FixRisk.Medium;
    public override bool RequiresAdmin => true;

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _run(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? new FixApplyResult { Outcome = FixApplyOutcome.Applied, Message = "TCP Receive Window Auto-Tuning was restored to Normal." }
            : new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = "Failed to restore TCP Receive Window Auto-Tuning.",
                ErrorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? null : result.StandardError
            };
    }
}