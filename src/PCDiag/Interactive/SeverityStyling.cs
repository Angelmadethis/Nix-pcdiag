using PCDiag.Core;
using Spectre.Console;

namespace PCDiag.Interactive;

/// <summary>
/// Pure helpers that map severities and risk scores to Spectre.Console colors and markup.
/// Kept free of any console dependency so they can be tested in isolation.
/// </summary>
public static class SeverityStyling
{
    public static Color ColorFor(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Healthy => Color.Green,
        DiagnosticSeverity.Info => Color.Cyan,
        DiagnosticSeverity.Suspicious => Color.Yellow,
        DiagnosticSeverity.Warning => Color.Orange1,
        DiagnosticSeverity.Critical => Color.Red,
        _ => Color.Grey
    };

    public static string MarkupFor(DiagnosticSeverity severity)
    {
        var color = ColorFor(severity).ToMarkup();
        return $"[{color}]{severity.ToString().ToUpperInvariant()}[/]";
    }

    public static string BadgeFor(DiagnosticSeverity severity)
    {
        var glyph = severity switch
        {
            DiagnosticSeverity.Healthy => "✓",
            DiagnosticSeverity.Info => "ℹ",
            DiagnosticSeverity.Suspicious => "⚠",
            DiagnosticSeverity.Warning => "⚠",
            DiagnosticSeverity.Critical => "✕",
            _ => "?"
        };
        var color = ColorFor(severity).ToMarkup();
        return $"[{color}]{glyph} {severity.ToString().ToUpperInvariant()}[/]";
    }

    public static Color RiskScoreColor(int score) => score switch
    {
        >= 70 => Color.Red,
        >= 40 => Color.Yellow,
        _ => Color.Green
    };

    public static string RiskScoreMarkup(int score)
    {
        var color = RiskScoreColor(score).ToMarkup();
        return $"[{color}]{score}/100[/]";
    }
}