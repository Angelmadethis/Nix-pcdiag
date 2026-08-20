using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Restarts the active network adapter using PowerShell's <c>Restart-NetAdapter</c>.
/// Reversible in the sense that restarting an adapter never changes its configuration,
/// but it does briefly drop connectivity, so it is a Medium-risk fix. The command
/// runner is injectable so tests can simulate success and failure.
/// </summary>
public sealed class RestartNetworkAdapterFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _run;

    public RestartNetworkAdapterFix(string problem, string adapterName, Func<CancellationToken, Task<CommandResult>>? run = null)
    {
        Problem = problem;
        AdapterName = adapterName;
        var script = $"Restart-NetAdapter -Name '{EscapeSingleQuotes(adapterName)}' -Confirm:$false";
        _run = run ?? (token => CommandRunner.RunAsync("powershell", $"-NoProfile -NonInteractive -Command \"{script}\"", cancellationToken: token));
    }

    /// <summary>The adapter that will be restarted.</summary>
    public string AdapterName { get; }

    public override string Id => "restart-network-adapter";
    public override string Title => $"Restart the network adapter ({AdapterName})";
    public override string Problem { get; }
    public override string Effect =>
        "This restarts the network adapter, which re-establishes the link and refreshes " +
        "the connection. No adapter configuration is changed, but connectivity will " +
        "briefly drop while the adapter restarts.";
    public override FixRisk Risk => FixRisk.Medium;
    public override bool RequiresAdmin => true;

    private static string EscapeSingleQuotes(string value) => value.Replace("'", "''");

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _run(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? new FixApplyResult { Outcome = FixApplyOutcome.Applied, Message = $"Network adapter '{AdapterName}' was successfully restarted." }
            : new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = $"Failed to restart the network adapter '{AdapterName}'.",
                ErrorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? null : result.StandardError
            };
    }
}