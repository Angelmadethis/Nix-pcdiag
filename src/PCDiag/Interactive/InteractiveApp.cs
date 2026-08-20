using PCDiag.Core;
using PCDiag.Fixes;
using PCDiag.Infrastructure;
using PCDiag.Inventory;
using PCDiag.Reporting;
using Spectre.Console;

namespace PCDiag.Interactive;

/// <summary>
/// Entry point of the interactive terminal UI. Running <c>pcdiag</c> always opens this.
/// Flow: title screen → "start scan" (ENTER) → progress → results table + risk score → menu.
/// </summary>
public static class InteractiveApp
{
    private enum AppAction
    {
        Exit,
        Details,
        Rerun,
        Info,
        Fix
    }

    private sealed record FixableFinding(DiagnosticResult Result, IDiagnosticCheck Check, IReadOnlyList<DiagnosticFix> Fixes);

    private sealed record MenuChoice(AppAction Action, string Display, IReadOnlyList<FixableFinding>? Fixes = null);

    /// <summary>
    /// Run the interactive UI. When <paramref name="console"/> is provided (tests) no real
    /// console handlers are attached. When the real console has redirected stdin (CI, piping)
    /// the start prompt is skipped and the app exits after printing results.
    /// When <paramref name="inventory"/> is provided (tests) it is reused instead of collected,
    /// and when <paramref name="checks"/> is provided it is used instead of the registry so
    /// tests do not touch the network.
    /// </summary>
    public static async Task<int> RunAsync(
        IAnsiConsole? console = null,
        SystemInventory? inventory = null,
        IReadOnlyList<IDiagnosticCheck>? checks = null)
    {
        var autoStart = console is null && Console.IsInputRedirected;
        var ansi = console ?? AnsiConsole.Console;
        var systemInventory = inventory ?? SystemInventoryCollector.Collect();

        using var cts = new CancellationTokenSource();
        var cancelHandler = new ConsoleCancelEventHandler((_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        });

        if (console is null)
            Console.CancelKeyPress += cancelHandler;

        try
        {
            ShowTitle(ansi);

            if (!autoStart)
            {
                ShowStartPanel(ansi);
                if (!await WaitForStartAsync(ansi, cts.Token))
                    return 0;
            }

            while (true)
            {
                var checkList = checks ?? CheckRegistry.GetAllChecks();
                var summary = await RunScanAsync(ansi, !autoStart, systemInventory, checkList, cts.Token);
                ShowResults(ansi, summary, !autoStart, checkList);

                if (autoStart)
                    return 0;

                var fixable = GetFixableFindings(summary, checkList);
                switch (SelectAction(ansi, fixable))
                {
                    case { Action: AppAction.Exit }:
                        return 0;
                    case { Action: AppAction.Details }:
                        await ShowDetails(ansi, summary, systemInventory, checkList, cts.Token);
                        break;
                    case { Action: AppAction.Info }:
                        await ShowSystemInfo(ansi, systemInventory, cts.Token);
                        break;
                    case { Action: AppAction.Fix, Fixes: { Count: > 0 } fixes }:
                        await ShowFixFlow(ansi, fixes, systemInventory, cts.Token);
                        break;
                    case { Action: AppAction.Rerun }:
                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            ansi.MarkupLine("\n[grey]Scan cancelled.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            ansi.MarkupLine($"\n[red]Unexpected error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        finally
        {
            if (console is null)
                Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static void ShowTitle(IAnsiConsole ansi)
    {
        ansi.Write(new FigletText("PCDiag").Color(Color.Cyan));
        ansi.MarkupLine("[grey]Windows PC Diagnostic Tool[/]");
        var version = typeof(InteractiveApp).Assembly.GetName().Version?.ToString() ?? "unknown";
        ansi.MarkupLine($"[grey]Version {version}[/]");
        ansi.WriteLine();
    }

    private static void ShowStartPanel(IAnsiConsole ansi)
    {
        var panel = new Panel(new Markup("[bold cyan]▶ START SCAN[/]") { Justification = Justify.Center })
        {
            Border = BoxBorder.Double,
            Padding = new Padding(2, 1)
        };
        ansi.Write(panel);
        ansi.MarkupLine("[grey]Press [bold green]ENTER[/] to start the scan[/]");
        ansi.MarkupLine("[grey]Press [bold red]ESC[/] to exit[/]");
        ansi.WriteLine();
    }

    private static async Task<bool> WaitForStartAsync(IAnsiConsole ansi, CancellationToken token)
    {
        while (true)
        {
            var key = await ansi.Input.ReadKeyAsync(true, token);
            if (key is null)
                return false;
            if (key.Value.Key == ConsoleKey.Enter)
                return true;
            if (key.Value.Key == ConsoleKey.Escape)
                return false;
        }
    }

    private static async Task<ScanSummary> RunScanAsync(
        IAnsiConsole ansi,
        bool interactive,
        SystemInventory inventory,
        IReadOnlyList<IDiagnosticCheck>? checks,
        CancellationToken token)
    {
        var context = new DiagnosticContext(
            mode: ScanMode.Standard,
            isAdministrator: PCDiag.Infrastructure.SystemInfo.IsRunningAsAdmin(),
            cancellationToken: token,
            inventory: inventory);

        var scanner = new Scanner(checks ?? CheckRegistry.GetAllChecks());
        var total = scanner.Checks.Count;

        if (total == 0)
        {
            ansi.MarkupLine("[yellow]No diagnostic checks are registered.[/]");
            return new ScanSummary(Array.Empty<DiagnosticResult>(), TimeSpan.Zero);
        }

        if (!interactive)
        {
            ansi.MarkupLine($"[grey]Running {total} checks...[/]");
            return await scanner.ScanAsync(context, (check, result) =>
                ansi.MarkupLine($"  [{SeverityStyling.ColorFor(result.Severity).ToMarkup()}]{result.Status.ToString().ToUpperInvariant()}[/] {check.CheckId} {check.Name}"));
        }

        return await ansi.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Running diagnostics", maxValue: total);
                var summary = await scanner.ScanAsync(context, (_, _) => task.Increment(1));
                task.StopTask();
                return summary;
            });
    }

    private static void ShowResults(IAnsiConsole ansi, ScanSummary summary, bool interactive, IReadOnlyList<IDiagnosticCheck> checks)
    {
        if (interactive)
            ansi.Clear();

        ansi.Write(ResultsTableBuilder.Build(summary, checkId => HasFixes(checks, checkId)));
        ansi.WriteLine();

        ansi.MarkupLine(
            $"[bold]Risk Score:[/] {SeverityStyling.RiskScoreMarkup(summary.RiskScore)}   " +
            $"[bold]Checks:[/] [grey]{summary.Total}[/]   " +
            $"[bold]Duration:[/] [grey]{summary.Duration.TotalSeconds:F1}s[/]   " +
            $"[bold]Max Severity:[/] [grey]{summary.MaxSeverity.ToString().ToUpperInvariant()}[/]");

        if (summary.Passed > 0)
            ansi.MarkupLine($"  [green]{summary.Passed} healthy[/]");
        if (summary.Finding > 0)
            ansi.MarkupLine($"  [yellow]{summary.Finding} finding{(summary.Finding == 1 ? "" : "s")}[/]");
        if (summary.Error > 0)
            ansi.MarkupLine($"  [red]{summary.Error} error{(summary.Error == 1 ? "" : "s")}[/]");
        if (summary.Skipped > 0)
            ansi.MarkupLine($"  [grey]{summary.Skipped} skipped[/]");
        if (summary.Unavailable > 0)
            ansi.MarkupLine($"  [grey]{summary.Unavailable} unavailable[/]");
        if (summary.PermissionDenied > 0)
            ansi.MarkupLine($"  [yellow]{summary.PermissionDenied} permission denied[/]");

        ansi.WriteLine();
    }

    private static MenuChoice SelectAction(IAnsiConsole ansi, IReadOnlyList<FixableFinding> fixable)
    {
        var choices = new List<MenuChoice>();
        if (fixable.Count > 0)
            choices.Add(new MenuChoice(AppAction.Fix, $"Fix all problems ({fixable.Count})", fixable));
        foreach (var finding in fixable)
            choices.Add(new MenuChoice(AppAction.Fix, $"[[ FIX ]] {finding.Result.Name}", new[] { finding }));
        choices.Add(new MenuChoice(AppAction.Details, "View check details"));
        choices.Add(new MenuChoice(AppAction.Info, "System info"));
        choices.Add(new MenuChoice(AppAction.Rerun, "Run scan again"));
        choices.Add(new MenuChoice(AppAction.Exit, "Exit"));

        var prompt = new SelectionPrompt<MenuChoice>()
            .Title("[bold cyan]What next?[/]")
            .UseConverter(c => c.Display)
            .AddChoices(choices);

        return ansi.Prompt(prompt);
    }

    private static async Task ShowSystemInfo(IAnsiConsole ansi, SystemInventory inventory, CancellationToken token)
    {
        ansi.Clear();
        new InventoryRenderer(ansi.Profile.Out.Writer).Print(inventory);
        ansi.MarkupLine("[grey]Press [bold green]ENTER[/] to return[/]");
        await ansi.Input.ReadKeyAsync(true, token);
    }

    private static async Task ShowDetails(
        IAnsiConsole ansi,
        ScanSummary summary,
        SystemInventory inventory,
        IReadOnlyList<IDiagnosticCheck> checks,
        CancellationToken token)
    {
        if (summary.Results.Count == 0)
            return;

        var pick = ansi.Prompt(
            new SelectionPrompt<DiagnosticResult>()
                .Title("[bold cyan]Select a check to inspect[/]")
                .UseConverter(r => $"{r.CheckId} — {r.Name} ({r.Status})")
                .AddChoices(summary.Results.OrderBy(r => r.Severity)));

        new TerminalRenderer().PrintDetailed(pick);
        ansi.WriteLine();

        if (checks.FirstOrDefault(c => c.CheckId == pick.CheckId) is IFixableCheck fixable
            && fixable.GetFixes(pick) is { Count: > 0 } fixes)
        {
            var choice = ansi.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]This finding has a recommended fix. Apply it?[/]")
                    .UseConverter(v => v == "apply" ? "[[ Apply Fix ]]" : "[[ Cancel ]]")
                    .AddChoices("apply", "cancel"));

            if (choice == "apply")
            {
                var context = new DiagnosticContext(
                    mode: ScanMode.Standard,
                    isAdministrator: SystemInfo.IsRunningAsAdmin(),
                    cancellationToken: token,
                    inventory: inventory);
                var outcome = await new FixExecutor().ExecuteAsync(fixable, fixes[0], context, pick, token);
                ShowFixOutcome(ansi, pick, fixes[0], outcome);
                ansi.MarkupLine("[grey]Press [bold green]ENTER[/] to return[/]");
                await ansi.Input.ReadKeyAsync(true, token);
                return;
            }
        }

        ansi.MarkupLine("[grey]Press [bold green]ENTER[/] to return[/]");
        await ansi.Input.ReadKeyAsync(true, token);
    }

    private static bool HasFixes(IReadOnlyList<IDiagnosticCheck> checks, string checkId)
    {
        return checks.FirstOrDefault(c => c.CheckId == checkId) is IFixableCheck;
    }

    private static IReadOnlyList<FixableFinding> GetFixableFindings(ScanSummary summary, IReadOnlyList<IDiagnosticCheck> checks)
    {
        return summary.Results
            .Where(r => r.Status == DiagnosticStatus.Finding)
            .Select(r => (Result: r, Check: checks.FirstOrDefault(c => c.CheckId == r.CheckId)))
            .Where(t => t.Check is IFixableCheck fixable && fixable.GetFixes(t.Result).Count > 0)
            .Select(t => new FixableFinding(t.Result, t.Check!, ((IFixableCheck)t.Check!).GetFixes(t.Result)))
            .ToList();
    }

    private static async Task ShowFixFlow(
        IAnsiConsole ansi,
        IReadOnlyList<FixableFinding> findings,
        SystemInventory inventory,
        CancellationToken token)
    {
        var context = new DiagnosticContext(
            mode: ScanMode.Standard,
            isAdministrator: SystemInfo.IsRunningAsAdmin(),
            cancellationToken: token,
            inventory: inventory);

        ansi.Clear();
        if (!ShowFixProposal(ansi, findings))
        {
            ansi.MarkupLine("[grey]Fixes cancelled.[/]");
            await ansi.Input.ReadKeyAsync(true, token);
            return;
        }

        foreach (var (result, check, fixes) in findings)
        {
            var fixable = (IFixableCheck)check;
            foreach (var fix in fixes)
            {
                var outcome = await new FixExecutor().ExecuteAsync(fixable, fix, context, result, token);
                ShowFixOutcome(ansi, result, fix, outcome);
            }
        }
    }

    private static bool ShowFixProposal(IAnsiConsole ansi, IReadOnlyList<FixableFinding> findings)
    {
        var lines = new List<string>();
        foreach (var (result, _, fixes) in findings)
        {
            foreach (var fix in fixes)
            {
                var glyph = result.Severity == DiagnosticSeverity.Critical ? "✕" : "⚠";
                var color = SeverityStyling.ColorFor(result.Severity).ToMarkup();
                var risk = fix.Risk switch
                {
                    FixRisk.Low => "LOW",
                    FixRisk.Medium => "MEDIUM",
                    _ => "HIGH"
                };

                lines.Add($"[bold {color}]{glyph} {result.Name.ToUpperInvariant()}[/]");
                lines.Add($"  [bold]Problem:[/] {Markup.Escape(fix.Problem)}");
                lines.Add($"  [bold]Fix:[/] {Markup.Escape(fix.Title)}");
                lines.Add($"  [bold]Effect:[/] {Markup.Escape(fix.Effect)}");
                lines.Add($"  [bold]Severity:[/] {SeverityStyling.MarkupFor(result.Severity)} (fix risk: [bold]{risk}[/])");
                lines.Add(string.Empty);
            }
        }

        var panel = new Panel(new Markup(string.Join("\n", lines)))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        ansi.Write(panel);
        ansi.WriteLine();

        var choice = ansi.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold cyan]Apply {findings.Sum(f => f.Fixes.Count)} fix(es)?[/]")
                .UseConverter(v => v == "apply" ? "[[ Apply ]]" : "[[ Cancel ]]")
                .AddChoices("apply", "cancel"));

        return choice == "apply";
    }

    private static void ShowFixOutcome(IAnsiConsole ansi, DiagnosticResult result, DiagnosticFix fix, FixExecutionResult outcome)
    {
        ansi.WriteLine();
        ansi.MarkupLine(outcome.Applied ? "[bold green]✓ FIX APPLIED[/]" : "[bold red]✕ FIX NOT APPLIED[/]");
        ansi.MarkupLine(Markup.Escape(outcome.Message));

        if (outcome.ErrorDetail is not null)
            ansi.MarkupLine($"[grey]{Markup.Escape(outcome.ErrorDetail)}[/]");

        if (outcome.RecheckResult is not null)
        {
            ansi.MarkupLine("[grey]Re-running diagnostic...[/]");
            ansi.MarkupLine(outcome.Resolved
                ? $"[bold green]✓ {Markup.Escape(result.Name)} issue resolved.[/]"
                : $"[bold yellow]✕ {Markup.Escape(result.Name)} issue persists.[/]");
        }
    }
}