using PCDiag.Checks.Windows;
using PCDiag.Core;

namespace PCDiag.Tests;

public class EnvironmentCheckTests
{
    [Fact]
    public async Task EnvironmentCheck_ShouldReturnValidResult()
    {
        var check = new EnvironmentCheck();

        var result = await check.ExecuteAsync(new DiagnosticContext());

        Assert.Equal("WIN-ENV-001", result.CheckId);
        Assert.Equal("Environment", result.Name);
        Assert.Equal(DiagnosticCategory.Windows, result.Category);
        Assert.NotEqual(DiagnosticStatus.Error, result.Status);
        Assert.NotEqual(DiagnosticStatus.Unavailable, result.Status);
        Assert.NotEmpty(result.Evidence);
        Assert.Contains(result.Evidence, e => e.Description == "OS Version");
        Assert.NotEqual(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void EnvironmentCheck_ShouldSupportSynchronousExecution()
    {
        var check = new EnvironmentCheck();

        var result = check.Execute(new DiagnosticContext());

        Assert.Equal("WIN-ENV-001", result.CheckId);
        Assert.NotEmpty(result.Evidence);
    }
}