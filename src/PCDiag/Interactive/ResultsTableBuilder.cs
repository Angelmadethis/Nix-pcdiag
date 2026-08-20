using PCDiag.Core;
using Spectre.Console;

namespace PCDiag.Interactive;

/// <summary>
/// Builds a Spectre.Console table describing a scan summary.
/// Pure construction; rendering is handled by the caller via <see cref="IAnsiConsole"/>.
/// </summary>
public static class ResultsTableBuilder
{
    /// <summary>
    /// Build the results table. When <paramref name="isFixable"/> is provided it is used
    /// to render a <c>[ FIX ]</c> button on each fixable finding row.
    /// </summary>
    public static Table Build(ScanSummary summary, Func<string, bool>? isFixable = null)
    {
        var table = new Table
        {
            Border = TableBorder.Rounded,
            Expand = true,
            Title = new TableTitle("SCAN RESULTS")
        };

        table.AddColumn(new TableColumn("STATUS").PadRight(2));
        table.AddColumn(new TableColumn("CHECK").PadRight(2));
        table.AddColumn(new TableColumn("NAME").PadRight(2));
        table.AddColumn(new TableColumn("SUMMARY"));
        if (isFixable is not null)
            table.AddColumn(new TableColumn("FIX").PadLeft(1));

        foreach (var result in summary.Results.OrderBy(r => r.Severity))
        {
            var statusColor = result.Status switch
            {
                DiagnosticStatus.Passed => "green",
                DiagnosticStatus.Finding => SeverityStyling.ColorFor(result.Severity).ToMarkup(),
                DiagnosticStatus.Error => "red",
                DiagnosticStatus.Skipped => "grey",
                DiagnosticStatus.Unavailable => "grey",
                DiagnosticStatus.PermissionDenied => "yellow",
                _ => "grey"
            };
            var status = $"[{statusColor}]{result.Status.ToString().ToUpperInvariant()}[/]";
            var severity = SeverityStyling.BadgeFor(result.Severity);

            if (isFixable is not null)
            {
                var fixable = result.Status == DiagnosticStatus.Finding && isFixable(result.CheckId);
                var fix = fixable ? "[bold green][[ FIX ]][/]" : "";
                table.AddRow(
                    new Markup(status),
                    new Markup(result.CheckId),
                    new Markup(result.Name),
                    new Markup($"{severity} {result.Summary.EscapeMarkup()}"),
                    new Markup(fix));
            }
            else
            {
                table.AddRow(
                    new Markup(status),
                    new Markup(result.CheckId),
                    new Markup(result.Name),
                    new Markup($"{severity} {result.Summary.EscapeMarkup()}"));
            }
        }

        return table;
    }
}