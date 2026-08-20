using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Resets the Winsock catalog by running <c>netsh winsock reset</c>. Reversible by
/// re-running the command; the catalog is rebuilt from defaults. Often requires a
/// restart to take full effect, which is why it is a Medium-risk fix. The command
/// runner is injectable so tests can simulate success and failure.
/// </summary>
public sealed class WinsockResetFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _run;

    public WinsockResetFix(string problem, Func<CancellationToken, Task<CommandResult>>? run = null)
    {
        Problem = problem;
        _run = run ?? (token => CommandRunner.RunAsync("netsh", "winsock reset", cancellationToken: token));
    }

    public override string Id => "winsock-reset";
    public override string Title => "Reset the Winsock catalog";
    public override string Problem { get; }
    public override string Effect =>
        "This rebuilds the Winsock catalog from defaults, clearing corrupted or misconfigured " +
        "LSP/network entries. Network settings and files are not modified, but a restart may be " +
        "required for the reset to fully take effect.";
    public override FixRisk Risk => FixRisk.Medium;
    public override bool RequiresAdmin => true;

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _run(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? new FixApplyResult { Outcome = FixApplyOutcome.Applied, Message = "Winsock catalog was successfully reset." }
            : new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = "Failed to reset the Winsock catalog.",
                ErrorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? null : result.StandardError
            };
    }
}