using PCDiag.Core;
using PCDiag.Interactive;
using Spectre.Console;

namespace PCDiag.Tests;

public class InteractiveStylingTests
{
    [Theory]
    [InlineData(DiagnosticSeverity.Healthy)]
    [InlineData(DiagnosticSeverity.Info)]
    [InlineData(DiagnosticSeverity.Suspicious)]
    [InlineData(DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticSeverity.Critical)]
    public void MarkupFor_ShouldIncludeUppercaseSeverity(DiagnosticSeverity severity)
    {
        var markup = SeverityStyling.MarkupFor(severity);

        Assert.Contains(severity.ToString().ToUpperInvariant(), markup);
        Assert.StartsWith("[", markup);
        Assert.EndsWith("[/]", markup);
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Healthy)]
    [InlineData(DiagnosticSeverity.Critical)]
    [InlineData(DiagnosticSeverity.Warning)]
    public void ColorFor_ShouldReturnKnownColor(DiagnosticSeverity severity)
    {
        var color = SeverityStyling.ColorFor(severity);

        Assert.False(color == default);
    }

    [Fact]
    public void RiskScoreColor_ShouldBeGreen_Below40()
    {
        Assert.Equal(Color.Green, SeverityStyling.RiskScoreColor(0));
        Assert.Equal(Color.Green, SeverityStyling.RiskScoreColor(39));
    }

    [Fact]
    public void RiskScoreColor_ShouldBeYellow_Between40And69()
    {
        Assert.Equal(Color.Yellow, SeverityStyling.RiskScoreColor(40));
        Assert.Equal(Color.Yellow, SeverityStyling.RiskScoreColor(69));
    }

    [Fact]
    public void RiskScoreColor_ShouldBeRed_Above70()
    {
        Assert.Equal(Color.Red, SeverityStyling.RiskScoreColor(70));
        Assert.Equal(Color.Red, SeverityStyling.RiskScoreColor(100));
    }
}