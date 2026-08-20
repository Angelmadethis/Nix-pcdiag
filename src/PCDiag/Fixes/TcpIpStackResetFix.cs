using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Resets the TCP/IP protocol stack by running <c>netsh int ip reset</c>. Reversible by
/// re-running the command; the stack is restored from defaults. Requires a restart to
/// take effect, which is why it is a High-risk fix. The command runner is injectable
/// so tests can simulate success and failure.
/// </summary>
public sealed class TcpIpStackResetFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _run;

    public TcpIpStackResetFix(string problem, Func<CancellationToken, Task<CommandResult>>? run = null)
    {
        Problem = problem;
        _run = run ?? (token => CommandRunner.RunAsync("netsh", "int ip reset", cancellationToken: token));
    }

    public override string Id => "tcp-ip-stack-reset";
    public override string Title => "Reset the TCP/IP protocol stack";
    public override string Problem { get; }
    public override string Effect =>
        "This resets the TCP/IP stack to default, clearing corrupted stack entries or " +
        "misapplied interface settings. A restart is required for the reset to take effect, " +
        "so connectivity will be interrupted.";
    public override FixRisk Risk => FixRisk.High;
    public override bool RequiresAdmin => true;

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _run(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? new FixApplyResult { Outcome = FixApplyOutcome.Applied, Message = "TCP/IP stack was successfully reset." }
            : new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = "Failed to reset the TCP/IP stack.",
                ErrorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? null : result.StandardError
            };
    }
}