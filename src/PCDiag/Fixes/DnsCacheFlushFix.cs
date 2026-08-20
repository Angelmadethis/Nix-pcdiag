using PCDiag.Infrastructure;

namespace PCDiag.Fixes;

/// <summary>
/// Flushes the Windows DNS resolver cache by running <c>ipconfig /flushdns</c>.
/// A low-risk, reversible fix that never touches DNS server settings. The command
/// runner is injectable so tests can simulate success and failure without invoking
/// the operating system.
/// </summary>
public sealed class DnsCacheFlushFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<CommandResult>> _run;

    public DnsCacheFlushFix(string problem, Func<CancellationToken, Task<CommandResult>>? run = null)
    {
        Problem = problem;
        _run = run ?? (token => CommandRunner.RunAsync("ipconfig", "/flushdns", cancellationToken: token));
    }

    public override string Id => "dns-flush-cache";
    public override string Title => "Flush the Windows DNS resolver cache";
    public override string Problem { get; }
    public override string Effect =>
        "This clears cached DNS records and forces Windows to resolve them again. No DNS server settings will be changed.";
    public override FixRisk Risk => FixRisk.Low;

    public override async Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _run(cancellationToken).ConfigureAwait(false);

        return result.Success
            ? new FixApplyResult { Outcome = FixApplyOutcome.Applied, Message = "DNS cache was successfully flushed." }
            : new FixApplyResult
            {
                Outcome = FixApplyOutcome.Failed,
                Message = "Failed to flush the DNS cache.",
                ErrorDetail = string.IsNullOrWhiteSpace(result.StandardError) ? null : result.StandardError
            };
    }
}