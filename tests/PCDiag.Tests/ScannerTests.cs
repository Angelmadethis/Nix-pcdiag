using PCDiag.Core;

namespace PCDiag.Tests;

public class ScannerTests
{
    [Fact]
    public async Task ScanAsync_ShouldRunAllChecks()
    {
        var checks = new IDiagnosticCheck[]
        {
            new SyncStubCheck("SYNC-001", TestResults.Healthy("SYNC-001")),
            new AsyncStubCheck("ASYNC-001", TestResults.Healthy("ASYNC-001")),
            new SyncStubCheck("WARN-001", TestResults.Healthy("WARN-001", DiagnosticSeverity.Warning))
        };

        var scanner = new Scanner(checks);
        var summary = await scanner.ScanAsync(new DiagnosticContext());

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.Passed);
        Assert.Equal(1, summary.Finding);
    }

    [Fact]
    public async Task ScanAsync_ShouldRunSyncAndAsyncChecks()
    {
        var checks = new IDiagnosticCheck[]
        {
            new SyncStubCheck("SYNC-001", TestResults.Healthy("SYNC-001")),
            new AsyncStubCheck("ASYNC-001", TestResults.Healthy("ASYNC-001"))
        };

        var scanner = new Scanner(checks);
        var summary = await scanner.ScanAsync(new DiagnosticContext());

        Assert.Equal(2, summary.Total);
        Assert.All(summary.Results, r => Assert.Equal(DiagnosticStatus.Passed, r.Status));
    }

    [Fact]
    public async Task RunAsync_ShouldReturnSingleCheckResult()
    {
        var scanner = new Scanner(new IDiagnosticCheck[]
        {
            new SyncStubCheck("ONE-001", TestResults.Healthy("ONE-001")),
            new SyncStubCheck("TWO-001", TestResults.Healthy("TWO-001"))
        });

        var result = await scanner.RunAsync("two-001", new DiagnosticContext());

        Assert.NotNull(result);
        Assert.Equal("TWO-001", result!.CheckId);
    }

    [Fact]
    public async Task RunAsync_UnknownCheck_ShouldReturnNull()
    {
        var scanner = new Scanner(Array.Empty<IDiagnosticCheck>());

        var result = await scanner.RunAsync("NOPE-001", new DiagnosticContext());

        Assert.Null(result);
    }

    [Fact]
    public async Task ScanAsync_ThrowingCheck_ShouldReturnErrorResultInsteadOfThrowing()
    {
        var scanner = new Scanner(new IDiagnosticCheck[] { new ThrowingCheck() });

        var summary = await scanner.ScanAsync(new DiagnosticContext());

        var result = Assert.Single(summary.Results);
        Assert.Equal(DiagnosticStatus.Error, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal("unexpected-error", error.Code);
    }

    [Fact]
    public async Task ScanAsync_PreCancelledToken_ShouldThrowOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var scanner = new Scanner(new IDiagnosticCheck[] { new SyncStubCheck("T-001", TestResults.Healthy("T-001")) });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scanner.ScanAsync(new DiagnosticContext(cancellationToken: cts.Token)));
    }

    [Fact]
    public async Task ScanAsync_CheckHonoursCancellation_ShouldPropagate()
    {
        using var cts = new CancellationTokenSource();

        var scanner = new Scanner(new IDiagnosticCheck[] { new SlowCheck() });
        var context = new DiagnosticContext(cancellationToken: cts.Token);
        var scanTask = scanner.ScanAsync(context);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanTask);
    }

    [Fact]
    public async Task ScanAsync_SlowCheck_ShouldReturnTimeoutError()
    {
        var scanner = new Scanner(new IDiagnosticCheck[] { new SlowCheck() });
        var context = new DiagnosticContext(defaultTimeout: TimeSpan.FromMilliseconds(50));

        var summary = await scanner.ScanAsync(context);

        var result = Assert.Single(summary.Results);
        Assert.Equal(DiagnosticStatus.Error, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal("timeout", error.Code);
    }

    [Fact]
    public async Task ScanAsync_AdminCheckWithoutElevation_ShouldReturnPermissionDenied()
    {
        var check = new SyncStubCheck("ADMIN-001", TestResults.Healthy("ADMIN-001"), requiresAdmin: true);
        var scanner = new Scanner(new IDiagnosticCheck[] { check });

        var summary = await scanner.ScanAsync(new DiagnosticContext(isAdministrator: false));

        var result = Assert.Single(summary.Results);
        Assert.Equal(DiagnosticStatus.PermissionDenied, result.Status);
    }

    [Fact]
    public async Task ScanAsync_QuickMode_ShouldSkipAdminChecks()
    {
        var adminCheck = new SyncStubCheck("ADMIN-001", TestResults.Healthy("ADMIN-001"), requiresAdmin: true);
        var normalCheck = new SyncStubCheck("NORMAL-001", TestResults.Healthy("NORMAL-001"));
        var scanner = new Scanner(new IDiagnosticCheck[] { adminCheck, normalCheck });

        var summary = await scanner.ScanAsync(new DiagnosticContext(mode: ScanMode.Quick, isAdministrator: false));

        Assert.Equal(1, summary.Total);
        Assert.Equal("NORMAL-001", summary.Results[0].CheckId);
    }

    [Fact]
    public async Task ScanAsync_UnavailableCheck_ShouldReturnUnavailableStatus()
    {
        var scanner = new Scanner(new IDiagnosticCheck[] { new UnavailableCheck() });

        var summary = await scanner.ScanAsync(new DiagnosticContext());

        var result = Assert.Single(summary.Results);
        Assert.Equal(DiagnosticStatus.Unavailable, result.Status);
    }
}