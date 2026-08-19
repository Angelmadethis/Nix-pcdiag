using PCDiag.Core;

namespace PCDiag.Reporting;

/// <summary>How scan results are grouped in the summary.</summary>
public enum ResultGrouping
{
    Category,
    Severity
}

/// <summary>
/// Renders diagnostic results to a text writer in rich (unicode) or plain (ASCII) mode.
/// Rich mode uses symbols (✓ ℹ ⚠ ✕); plain mode uses bracketed ASCII labels ([HEALTHY] ...).
/// </summary>
public sealed class TerminalRenderer
{
    private readonly TextWriter _output;
    private readonly bool _plain;

    public TerminalRenderer(bool plain = false, TextWriter? output = null)
    {
        _plain = plain;
        _output = output ?? Console.Out;
    }

    private sealed record SeverityStyle(string Symbol, string Label, ConsoleColor Color)
    {
        public string Badge(bool plain) => plain ? $"[{Label}]" : $"{Symbol} {Label}";
    }

    private static readonly IReadOnlyDictionary<DiagnosticSeverity, SeverityStyle> Styles =
        new Dictionary<DiagnosticSeverity, SeverityStyle>
        {
            [DiagnosticSeverity.Healthy] = new("✓", "HEALTHY", ConsoleColor.Green),
            [DiagnosticSeverity.Info] = new("ℹ", "INFO", ConsoleColor.Cyan),
            [DiagnosticSeverity.Suspicious] = new("⚠", "SUSPICIOUS", ConsoleColor.Yellow),
            [DiagnosticSeverity.Warning] = new("⚠", "WARNING", ConsoleColor.DarkYellow),
            [DiagnosticSeverity.Critical] = new("✕", "CRITICAL", ConsoleColor.Red)
        };

    private static SeverityStyle StyleFor(DiagnosticSeverity severity)
        => Styles.GetValueOrDefault(severity, Styles[DiagnosticSeverity.Info]);

    /// <summary>Render a severity as a badge, honoring rich/plain mode.</summary>
    public string RenderSeverity(DiagnosticSeverity severity)
        => StyleFor(severity).Badge(_plain);

    private string Bullet => _plain ? "-" : "•";
    private string ErrorMark => _plain ? "X" : "✕";
    private string RuleLine => _plain ? new string('-', 40) : "────────────────────────────────────────";
    private string SectionLine => _plain ? new string('-', 36) : "────────────────────────────────";

    /// <summary>Print the pcdiag version line.</summary>
    public void PrintVersion(string version)
    {
        _output.WriteLine();
        _output.WriteLine($"pcdiag {version}");
    }

    /// <summary>Print a scan summary grouped by category or severity, with footer.</summary>
    public void PrintScanSummary(ScanSummary summary, ResultGrouping grouping = ResultGrouping.Category)
    {
        _output.WriteLine();
        WriteLineColored("PCDIAG SYSTEM SCAN", ConsoleColor.White);
        _output.WriteLine(RuleLine);

        if (summary.Results.Count == 0)
        {
            WriteLineColored("  No checks ran.", ConsoleColor.Gray);
        }
        else
        {
            foreach (var (key, items) in GroupResults(summary.Results, grouping))
            {
                WriteLineColored($"  {key.ToUpperInvariant()}", ConsoleColor.White);
                foreach (var result in items.OrderBy(r => r.Severity))
                {
                    PrintResultRow(result);
                }
                _output.WriteLine();
            }
        }

        PrintFooter(summary);
    }

    /// <summary>Print a line announcing the start of a scan.</summary>
    public void PrintProgressStart(int checkCount)
    {
        WriteLineColored($"  Running {checkCount} checks...", ConsoleColor.Gray);
    }

    /// <summary>Print a live progress line for a completed check.</summary>
    public void PrintProgress(IDiagnosticCheck check, DiagnosticResult result, TimeSpan scanElapsed)
    {
        var style = StyleFor(result.Severity);
        WriteColored($"  {style.Badge(_plain),-12}", style.Color);
        _output.Write($" {check.CheckId,-12} {check.Name,-30} ");
        WriteColored(result.Status.ToString().ToUpperInvariant(), ConsoleColor.Gray);
        _output.WriteLine($"  {result.Duration.TotalMilliseconds:F0}ms  [elapsed {scanElapsed.TotalSeconds:F1}s]");
    }

    /// <summary>Print the detailed result view for a single check.</summary>
    public void PrintDetailed(DiagnosticResult result)
    {
        var style = StyleFor(result.Severity);

        _output.WriteLine();
        WriteLineColored("PCDIAG CHECK", ConsoleColor.White);
        _output.WriteLine(RuleLine);

        _output.Write("  CHECK        ");
        WriteLineColored($"{result.Name} ({result.CheckId})", ConsoleColor.White);
        _output.WriteLine($"  CATEGORY     {result.Category}");
        _output.Write("  STATUS       ");
        WriteLineColored(style.Badge(_plain), style.Color);
        _output.Write("  SEVERITY     ");
        WriteLineColored(result.Severity.ToString(), style.Color);
        _output.WriteLine($"  CONFIDENCE   {result.Confidence:P0}");
        _output.WriteLine($"  DURATION     {result.Duration.TotalMilliseconds:F0}ms");
        _output.WriteLine();

        PrintSection("WHAT WAS DETECTED", result.Summary);

        if (result.Evidence.Count > 0)
        {
            PrintSectionHeader("EVIDENCE");
            foreach (var item in result.Evidence)
            {
                _output.Write($"  {Bullet} ");
                _output.Write($"{item.Description}: ");
                _output.Write(item.Value);
                if (item.ExpectedValue is not null)
                    _output.Write($" (expected: {item.ExpectedValue})");
                if (item.Source is not null)
                    WriteColored($" [{item.Source}]", ConsoleColor.DarkGray);
                _output.WriteLine();
            }
            _output.WriteLine();
        }

        if (!string.IsNullOrEmpty(result.Detail))
            PrintSection("WHY IT MATTERS", result.Detail);

        if (result.PossibleCauses.Count > 0)
            PrintSection("POSSIBLE CAUSES", result.PossibleCauses);

        if (result.Recommendations.Count > 0)
        {
            PrintSectionHeader("RECOMMENDED ACTIONS");
            int step = 1;
            foreach (var rec in result.Recommendations.OrderBy(r => r.Priority))
            {
                _output.Write($"  {step}. {rec.Text}");
                if (rec.Automatable)
                    WriteColored(" [automatable]", ConsoleColor.DarkGray);
                if (rec.RequiresAdmin)
                    WriteColored(" [requires admin]", ConsoleColor.DarkYellow);
                _output.WriteLine();
                step++;
            }
            _output.WriteLine();
        }

        if (result.Limitations.Count > 0)
            PrintSection("LIMITATIONS", result.Limitations);

        if (result.Errors.Count > 0)
        {
            PrintSectionHeader("ERRORS");
            foreach (var error in result.Errors)
            {
                WriteLineColored($"  {ErrorMark} [{error.Code}] {error.Message}", ConsoleColor.Red);
            }
            _output.WriteLine();
        }
    }

    /// <summary>Print a list of all available checks grouped by category.</summary>
    public void PrintCheckList(IReadOnlyList<IDiagnosticCheck> checks)
    {
        _output.WriteLine();
        WriteLineColored("AVAILABLE CHECKS", ConsoleColor.White);
        _output.WriteLine(RuleLine);

        foreach (var category in checks.GroupBy(c => c.Category).OrderBy(g => g.Key))
        {
            WriteLineColored($"  {category.Key.ToString().ToUpperInvariant()}", ConsoleColor.White);
            foreach (var check in category.OrderBy(c => c.CheckId))
            {
                _output.Write($"    {check.CheckId,-12} {check.Name,-30}");
                if (check.RequiresAdmin)
                    WriteColored(" [admin]", ConsoleColor.DarkYellow);
                _output.WriteLine();
            }
            _output.WriteLine();
        }
    }

    /// <summary>Print an error for an unknown check along with the available checks.</summary>
    public void PrintCheckNotFound(string nameOrId, IReadOnlyList<IDiagnosticCheck> checks)
    {
        _output.WriteLine();
        WriteLineColored($"  Check not found: {nameOrId}", ConsoleColor.Red);
        _output.WriteLine();
        WriteLineColored("  Available checks:", ConsoleColor.White);
        foreach (var check in checks)
            _output.WriteLine($"    {check.CheckId,-12} {check.Name}");
        _output.WriteLine();
    }

    private static IEnumerable<(string Key, IEnumerable<DiagnosticResult> Items)> GroupResults(
        IReadOnlyList<DiagnosticResult> results,
        ResultGrouping grouping)
    {
        if (grouping == ResultGrouping.Severity)
        {
            return results
                .GroupBy(r => r.Severity)
                .OrderByDescending(g => g.Key)
                .Select(g => (g.Key.ToString(), (IEnumerable<DiagnosticResult>)g));
        }

        return results
            .GroupBy(r => r.Category)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key.ToString(), (IEnumerable<DiagnosticResult>)g));
    }

    private void PrintResultRow(DiagnosticResult result)
    {
        var style = StyleFor(result.Severity);
        WriteColored($"  {style.Badge(_plain),-12}", style.Color);
        _output.Write($" {result.Name,-34}");
        WriteLineColored(result.Status.ToString().ToUpperInvariant(), ConsoleColor.Gray);
    }

    private void PrintFooter(ScanSummary summary)
    {
        _output.WriteLine(RuleLine);

        foreach (var severity in Enum.GetValues<DiagnosticSeverity>().OrderByDescending(s => s))
        {
            int count = summary.Results.Count(r => r.Severity == severity && IsCountable(r));
            if (count > 0)
                WriteLineColored($"  {count} {severity}", StyleFor(severity).Color);
        }

        var statusNotes = new List<string>();
        if (summary.Error > 0) statusNotes.Add($"{summary.Error} errors");
        if (summary.Skipped > 0) statusNotes.Add($"{summary.Skipped} skipped");
        if (summary.Unavailable > 0) statusNotes.Add($"{summary.Unavailable} unavailable");
        if (summary.PermissionDenied > 0) statusNotes.Add($"{summary.PermissionDenied} permission denied");
        if (statusNotes.Count > 0)
            WriteLineColored($"  {string.Join(", ", statusNotes)}", ConsoleColor.DarkGray);

        _output.WriteLine();
        _output.Write("  Risk Score: ");
        int score = summary.RiskScore;
        var scoreColor = score switch
        {
            >= 70 => ConsoleColor.Red,
            >= 40 => ConsoleColor.Yellow,
            _ => ConsoleColor.Green
        };
        WriteLineColored($"{score}/100", scoreColor);
        _output.WriteLine($"  Scan completed in {summary.Duration.TotalSeconds:F1}s  (max severity: {summary.MaxSeverity})");
        _output.WriteLine();
    }

    private static bool IsCountable(DiagnosticResult result)
        => result.Status != DiagnosticStatus.Error
           && result.Status != DiagnosticStatus.Skipped
           && result.Status != DiagnosticStatus.Unavailable
           && result.Status != DiagnosticStatus.PermissionDenied;

    private void PrintSectionHeader(string title)
    {
        WriteLineColored($"  {title}", ConsoleColor.White);
        _output.WriteLine($"  {SectionLine}");
    }

    private void PrintSection(string title, string text)
    {
        PrintSectionHeader(title);
        WrapText("  ", text);
        _output.WriteLine();
    }

    private void PrintSection(string title, IReadOnlyList<string> items)
    {
        PrintSectionHeader(title);
        foreach (var item in items)
            _output.WriteLine($"  {Bullet} {item}");
        _output.WriteLine();
    }

    private void WrapText(string indent, string text)
    {
        var words = text.Split(' ');
        var line = indent;
        foreach (var word in words)
        {
            if (line.Length + word.Length + 1 > 76)
            {
                _output.WriteLine(line);
                line = indent + word;
            }
            else
            {
                line += (line.Length > indent.Length ? " " : "") + word;
            }
        }
        if (line.Length > indent.Length)
            _output.WriteLine(line);
    }

    private void WriteColored(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        _output.Write(text);
        Console.ResetColor();
    }

    private void WriteLineColored(string text, ConsoleColor color)
    {
        WriteColored(text, color);
        _output.WriteLine();
    }
}