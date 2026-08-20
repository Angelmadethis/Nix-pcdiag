using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Releases and renews the DHCP lease by running <c>ipconfig /release</c> followed by
/// <c>ipconfig /renew</c>. Reversible: DHCP leases are re-obtained automatically and no
/// settings are changed. Connectivity drops briefly while the lease is renewed. The
/// command runner is injectable so tests can simulate success and failure.
/// </summary>
public sealed class DhcpRenewFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _release;
    private readonly Func<CancellationToken, Task<CommandResult>> _renew;

    public DhcpRenewFix(
        string problem,
        Func<CancellationToken, Task<CommandResult>>? release = null,
        Func<CancellationToken, Task<CommandResult>>? renew = null)
    {
        Problem = problem;
        _release = release ?? (token => CommandRunner.RunAsync("ipconfig", "/release", cancellationToken: token));
        _renew = renew ?? (token => CommandRunner.RunAsync("ipconfig", "/renew", cancellationToken: token));
    }

    public override string Id => "dhcp-release-renew";
    public override string Title => "Release and renew the DHCP lease";
    public override string Problem { get; }
    public override string Effect =>
        "This releases the current DHCP lease and requests a fresh one from the router. " +
        "A new IP address is normally obtained automatically and no settings are changed, " +
        "but connectivity drops briefly during the renewal.";
    public override FixRisk Risk => FixRisk.Medium;
    public override bool RequiresAdmin => true;

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var release = await _release(cancellationToken).ConfigureAwait(false);
        if (!release.Success)
        {
            return new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = "Failed to release the DHCP lease.",
                ErrorDetail = string.IsNullOrWhiteSpace(release.StandardError) ? null : release.StandardError
            };
        }

        var renew = await _renew(cancellationToken).ConfigureAwait(false);
        if (!renew.Success)
        {
            return new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = "Lease released, but failed to renew the DHCP lease.",
                ErrorDetail = string.IsNullOrWhiteSpace(renew.StandardError) ? null : renew.StandardError
            };
        }

        return new FixApplyResult
        {
            Outcome = FixApplyOutcome.Applied,
            Message = "DHCP lease was successfully released and renewed."
        };
    }
}