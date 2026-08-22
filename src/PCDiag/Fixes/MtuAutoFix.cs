using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Resets the interface MTU to the Windows default (1500) by running
/// <c>netsh interface ipv4 set subinterface "{adapter}" mtu=1500 store=persistent</c>.
/// Reversible; the same command can set any other MTU value. No restart is required.
/// The command runner is injectable so tests can simulate success and failure.
/// </summary>
public sealed class MtuAutoFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _run;

    public MtuAutoFix(string problem, string adapterName, Func<CancellationToken, Task<CommandResult>>? run = null)
    {
        Problem = problem;
        AdapterName = adapterName;
        _run = run ?? (token => CommandRunner.RunAsync("netsh",
            $"interface ipv4 set subinterface \"{adapterName}\" mtu=1500 store=persistent",
            cancellationToken: token));
    }

    public override string Id => "mtu-reset-default";
    public override string Title => $"Reset MTU to Default (1500) on {AdapterName}";
    public override string Problem { get; }
    public override string Effect =>
        "This resets the interface MTU to the Windows default (1500 bytes). " +
        "The change applies immediately and no restart is required; it only affects " +
        "the MTU setting on this adapter, nothing else. If the current MTU was set " +
        "for a reason (e.g. PPPoE or jumbo frames), this may need to be reconfigured.";
    public override FixRisk Risk => FixRisk.Low;
    public override bool RequiresAdmin => true;
    public string AdapterName { get; }

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _run(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? new FixApplyResult { Outcome = FixApplyOutcome.Applied, Message = $"MTU on {AdapterName} was reset to 1500 (default)." }
            : new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = $"Failed to reset MTU on {AdapterName}.",
                ErrorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? null : result.StandardError
            };
    }
}
