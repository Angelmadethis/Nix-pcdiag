using PCDiag.Checks.Network;
using PCDiag.Core;
using PCDiag.Fixes;

namespace PCDiag.Tests.Fixes;

public class CheckFixWiringTests
{
    private static DiagnosticResult Result(
        string checkId,
        string name,
        DiagnosticSeverity severity,
        DiagnosticStatus status = DiagnosticStatus.Finding,
        params DiagnosticEvidence[] evidence)
        => new()
        {
            CheckId = checkId,
            Name = name,
            Category = DiagnosticCategory.Network,
            Severity = severity,
            Status = status,
            Summary = "Test summary.",
            Evidence = evidence
        };

    private static DiagnosticEvidence AdapterEvidence(string adapter = "Ethernet") => new()
    {
        Description = "Active Adapter",
        Value = $"{adapter} (192.168.1.5)",
        Source = "SystemInventory.Network"
    };

    private static DiagnosticEvidence AutotuningEvidence(string value = "Disabled") => new()
    {
        Description = "Receive Window Auto-Tuning",
        Value = value,
        Source = "MSFT_NetTCPSetting"
    };

    [Fact]
    public void Gateway_Healthy_ShouldOfferNoFixes()
    {
        var check = new GatewayCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Healthy, DiagnosticStatus.Passed));

        Assert.Empty(fixes);
    }

    [Fact]
    public void Gateway_Unreachable_ShouldOfferRestartRenewAndWinsock()
    {
        var check = new GatewayCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Critical, evidence: AdapterEvidence()));

        Assert.Contains(fixes, f => f is RestartNetworkAdapterFix);
        Assert.Contains(fixes, f => f is DhcpRenewFix);
        Assert.Contains(fixes, f => f is WinsockResetFix);
        var restart = Assert.Single(fixes.OfType<RestartNetworkAdapterFix>());
        Assert.Equal("Ethernet", restart.AdapterName);
    }

    [Fact]
    public void Gateway_WithoutAdapterEvidence_ShouldOfferRenewOnly()
    {
        var check = new GatewayCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Warning));

        Assert.Contains(fixes, f => f is DhcpRenewFix);
        Assert.DoesNotContain(fixes, f => f is RestartNetworkAdapterFix);
    }

    [Fact]
    public void PacketLoss_Healthy_ShouldOfferNoFixes()
    {
        var check = new PacketLossCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Healthy, DiagnosticStatus.Passed));

        Assert.Empty(fixes);
    }

    [Fact]
    public void PacketLoss_Suspicious_ShouldOfferRestart()
    {
        var check = new PacketLossCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Suspicious, evidence: AdapterEvidence()));

        Assert.Contains(fixes, f => f is RestartNetworkAdapterFix);
        Assert.DoesNotContain(fixes, f => f is DhcpRenewFix);
        Assert.DoesNotContain(fixes, f => f is WinsockResetFix);
    }

    [Fact]
    public void PacketLoss_Unreachable_ShouldOfferAllFixes()
    {
        var check = new PacketLossCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Critical, evidence: AdapterEvidence()));

        Assert.Contains(fixes, f => f is RestartNetworkAdapterFix);
        Assert.Contains(fixes, f => f is DhcpRenewFix);
        Assert.Contains(fixes, f => f is WinsockResetFix);
    }

    [Fact]
    public void TcpHealth_Healthy_ShouldOfferNoFixes()
    {
        var check = new TcpHealthCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Healthy, DiagnosticStatus.Passed));

        Assert.Empty(fixes);
    }

    [Fact]
    public void TcpHealth_WithAutotuningOff_ShouldOfferRestore()
    {
        var check = new TcpHealthCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Suspicious, evidence: AutotuningEvidence("Disabled")));

        Assert.Contains(fixes, f => f is AutotuningRestoreFix);
        Assert.Contains(fixes, f => f is WinsockResetFix);
    }

    [Fact]
    public void TcpHealth_WithAutotuningNormal_ShouldOfferWinsockOnly()
    {
        var check = new TcpHealthCheck();

        var fixes = check.GetFixes(Result(check.CheckId, check.Name, DiagnosticSeverity.Warning, evidence: AutotuningEvidence("Normal")));

        Assert.DoesNotContain(fixes, f => f is AutotuningRestoreFix);
        Assert.Contains(fixes, f => f is WinsockResetFix);
    }
}