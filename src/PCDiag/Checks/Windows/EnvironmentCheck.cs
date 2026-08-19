using PCDiag.Core;
using PCDiag.Infrastructure;

namespace PCDiag.Checks.Windows;

/// <summary>
/// Example check that collects basic OS environment information.
/// Demonstrates the synchronous check path.
/// </summary>
public sealed class EnvironmentCheck : DiagnosticCheck
{
    public override string CheckId => "WIN-ENV-001";
    public override string Name => "Environment";
    public override DiagnosticCategory Category => DiagnosticCategory.Windows;
    public override string Description => "Collects basic OS environment information.";

    protected override DiagnosticResult Run(DiagnosticContext context)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new()
            {
                Description = "OS Version",
                Value = Environment.OSVersion.VersionString,
                Source = "Environment.OSVersion"
            },
            new()
            {
                Description = "Machine Name",
                Value = Environment.MachineName,
                Source = "Environment.MachineName"
            },
            new()
            {
                Description = "64-bit OS",
                Value = Environment.Is64BitOperatingSystem ? "Yes" : "No",
                Source = "Environment.Is64BitOperatingSystem"
            },
            new()
            {
                Description = "Logical Processors",
                Value = Environment.ProcessorCount.ToString(),
                Source = "Environment.ProcessorCount"
            },
            new()
            {
                Description = "Running as Administrator",
                Value = SystemInfo.IsRunningAsAdmin() ? "Yes" : "No",
                Source = "SystemInfo.IsRunningAsAdmin"
            },
            new()
            {
                Description = "System Uptime",
                Value = $"{TimeSpan.FromMilliseconds(Environment.TickCount64).TotalDays:F1} days",
                Source = "Environment.TickCount64"
            }
        };

        return BuildResult(
            DiagnosticSeverity.Healthy,
            DiagnosticStatus.Passed,
            "Basic environment information collected successfully.",
            evidence: evidence,
            limitations: new[]
            {
                "Informational only; this check does not assess hardware health or performance."
            });
    }
}