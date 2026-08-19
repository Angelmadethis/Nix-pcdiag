using PCDiag.Core;

namespace PCDiag.Tests;

internal static class TestResults
{
    public static DiagnosticResult Healthy(string checkId, DiagnosticSeverity severity = DiagnosticSeverity.Healthy)
        => new()
        {
            CheckId = checkId,
            Name = checkId,
            Category = DiagnosticCategory.Windows,
            Severity = severity,
            Status = severity == DiagnosticSeverity.Healthy ? DiagnosticStatus.Passed : DiagnosticStatus.Finding,
            Summary = "Test result"
        };
}

/// <summary>A synchronous stub check with a configurable result.</summary>
internal sealed class SyncStubCheck : DiagnosticCheck
{
    public override string CheckId { get; }
    public override string Name { get; }
    public override DiagnosticCategory Category { get; }
    public override string Description { get; } = "Synchronous stub check.";
    public override bool RequiresAdmin { get; }

    public DiagnosticResult Result { get; init; }

    public SyncStubCheck(string checkId, DiagnosticResult result, bool requiresAdmin = false)
    {
        CheckId = checkId;
        Name = $"Sync {checkId}";
        Category = DiagnosticCategory.Windows;
        Result = result;
        RequiresAdmin = requiresAdmin;
    }

    protected override DiagnosticResult Run(DiagnosticContext context) => Result;
}

/// <summary>An asynchronous stub check with a configurable result.</summary>
internal sealed class AsyncStubCheck : DiagnosticCheck
{
    public override string CheckId { get; }
    public override string Name { get; }
    public override DiagnosticCategory Category { get; }
    public override string Description { get; } = "Asynchronous stub check.";
    public override bool RequiresAdmin { get; }

    public DiagnosticResult Result { get; init; }

    public AsyncStubCheck(string checkId, DiagnosticResult result, bool requiresAdmin = false)
    {
        CheckId = checkId;
        Name = $"Async {checkId}";
        Category = DiagnosticCategory.Windows;
        Result = result;
        RequiresAdmin = requiresAdmin;
    }

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return Result;
    }
}

/// <summary>A check that always throws.</summary>
internal sealed class ThrowingCheck : DiagnosticCheck
{
    public override string CheckId => "THROW-001";
    public override string Name => "Throwing Check";
    public override DiagnosticCategory Category => DiagnosticCategory.Windows;
    public override string Description => "Always throws.";

    protected override Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("deliberate test exception");
}

/// <summary>A check that never completes on its own (for timeout tests).</summary>
internal sealed class SlowCheck : DiagnosticCheck
{
    public override string CheckId => "SLOW-001";
    public override string Name => "Slow Check";
    public override DiagnosticCategory Category => DiagnosticCategory.Windows;
    public override string Description => "Never completes on its own.";

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("should never complete");
    }
}

/// <summary>A check that reports itself as unavailable.</summary>
internal sealed class UnavailableCheck : DiagnosticCheck
{
    public override string CheckId => "UNAVAIL-001";
    public override string Name => "Unavailable Check";
    public override DiagnosticCategory Category => DiagnosticCategory.Windows;
    public override string Description => "Always unavailable.";

    protected override DiagnosticResult Run(DiagnosticContext context)
        => Unavailable("This check is not available in this environment.");
}