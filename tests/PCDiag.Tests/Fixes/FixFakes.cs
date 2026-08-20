using PCDiag.Core;
using PCDiag.Fixes;

namespace PCDiag.Tests.Fixes;

/// <summary>A check that returns a configurable result and exposes configurable fixes.</summary>
internal sealed class FakeFixableCheck : DiagnosticCheck, IFixableCheck
{
    public override string CheckId { get; }
    public override string Name { get; }
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description => "Fixable stub check.";

    public DiagnosticResult Result { get; }
    public DiagnosticResult? ResultAfterFix { get; }
    public List<DiagnosticFix> Fixes { get; } = new();
    public int ExecuteCount { get; private set; }

    public FakeFixableCheck(string checkId, DiagnosticResult result, DiagnosticResult? resultAfterFix = null)
    {
        CheckId = checkId;
        Name = $"Fixable {checkId}";
        Result = result;
        ResultAfterFix = resultAfterFix;
    }

    public IReadOnlyList<DiagnosticFix> GetFixes(DiagnosticResult result) => Fixes;

    protected override Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        ExecuteCount++;
        return Task.FromResult(ExecuteCount == 1 || ResultAfterFix is null ? Result : ResultAfterFix);
    }
}

/// <summary>A fix with injectable apply and verify behavior.</summary>
internal sealed class FakeFix : DiagnosticFix
{
    private readonly Func<CancellationToken, Task<FixApplyResult>> _apply;
    private readonly Func<CancellationToken, Task<bool?>>? _verify;

    public override string Id => "fake-fix";
    public override string Title => "Apply the fake fix";
    public override string Problem => "A test problem was detected.";
    public override string Effect => "The fake fix corrects the test problem.";
    public override FixRisk Risk => FixRisk.Low;
    public override bool RequiresAdmin { get; }

    public int ApplyCount { get; private set; }

    public FakeFix(
        FixApplyOutcome outcome = FixApplyOutcome.Applied,
        string message = "The fake fix was applied.",
        string? errorDetail = null,
        bool? verify = null,
        bool requiresAdmin = false)
    {
        _apply = _ =>
        {
            ApplyCount++;
            return Task.FromResult(new FixApplyResult { Outcome = outcome, Message = message, ErrorDetail = errorDetail });
        };
        if (verify is bool v)
            _verify = _ => Task.FromResult<bool?>(v);
        RequiresAdmin = requiresAdmin;
    }

    public override Task<FixApplyResult> ApplyAsync(CancellationToken cancellationToken = default) => _apply(cancellationToken);

    public override Task<bool?> VerifyAsync(CancellationToken cancellationToken = default)
        => _verify?.Invoke(cancellationToken) ?? base.VerifyAsync(cancellationToken);
}

internal static class FixTestResults
{
    public static DiagnosticResult Finding(string checkId, DiagnosticSeverity severity = DiagnosticSeverity.Suspicious)
        => new()
        {
            CheckId = checkId,
            Name = $"Fixable {checkId}",
            Category = DiagnosticCategory.Network,
            Severity = severity,
            Status = DiagnosticStatus.Finding,
            Summary = "A test problem was detected."
        };

    public static DiagnosticResult Healthy(string checkId)
        => new()
        {
            CheckId = checkId,
            Name = $"Fixable {checkId}",
            Category = DiagnosticCategory.Network,
            Severity = DiagnosticSeverity.Healthy,
            Status = DiagnosticStatus.Passed,
            Summary = "All clear."
        };
}