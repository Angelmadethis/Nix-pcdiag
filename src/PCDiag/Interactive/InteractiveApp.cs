using PCDiag.Core;
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
        Info
    }

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
                var summary = await RunScanAsync(ansi, !autoStart, systemInventory, checks, cts.Token);
                ShowResults(ansi, summary, !autoStart);

                if (autoStart)
                    return 0;

                switch (SelectAction(ansi))
                {
                    case AppAction.Exit:
                        return 0;
                    case AppAction.Details:
                        await ShowDetails(ansi, summary, cts.Token);
                        break;
                    case AppAction.Info:
                        await ShowSystemInfo(ansi, systemInventory, cts.Token);
                        break;
                    case AppAction.Rerun:
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

    private static void ShowResults(IAnsiConsole ansi, ScanSummary summary, bool interactive)
    {
        if (interactive)
            ansi.Clear();

        ansi.Write(ResultsTableBuilder.Build(summary));
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

    private static AppAction SelectAction(IAnsiConsole ansi)
    {
        var prompt = new SelectionPrompt<string>()
            .Title("[bold cyan]What next?[/]")
            .AddChoices("View check details", "System info", "Run scan again", "Exit");

        return ansi.Prompt(prompt) switch
        {
            "View check details" => AppAction.Details,
            "System info" => AppAction.Info,
            "Run scan again" => AppAction.Rerun,
            _ => AppAction.Exit
        };
    }

    private static async Task ShowSystemInfo(IAnsiConsole ansi, SystemInventory inventory, CancellationToken token)
    {
        ansi.Clear();
        new InventoryRenderer(ansi.Profile.Out.Writer).Print(inventory);
        ansi.MarkupLine("[grey]Press [bold green]ENTER[/] to return[/]");
        await ansi.Input.ReadKeyAsync(true, token);
    }

    private static async Task ShowDetails(IAnsiConsole ansi, ScanSummary summary, CancellationToken token)
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
        ansi.MarkupLine("[grey]Press [bold green]ENTER[/] to return[/]");
        await ansi.Input.ReadKeyAsync(true, token);
    }
}