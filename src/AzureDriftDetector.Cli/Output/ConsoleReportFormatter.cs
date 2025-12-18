using AzureDriftDetector.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AzureDriftDetector.Cli.Output;

public sealed class ConsoleReportFormatter : IReportFormatter
{
    public string Format(DriftReport report)
    {
        // For file output, we need to return a string
        // Render to a string using a console that writes to StringWriter
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = ColorSystemSupport.TrueColor
        });

        RenderReport(console, report);
        return writer.ToString();
    }

    public void WriteToConsole(DriftReport report)
    {
        // For direct console output with full interactivity
        RenderReport(AnsiConsole.Console, report);
    }

    private static void RenderReport(IAnsiConsole console, DriftReport report)
    {
        console.WriteLine();

        // Header Panel
        var headerPanel = new Panel(
            new Markup($"[bold]Azure Resource Drift Report[/]"))
            .Border(BoxBorder.Double)
            .BorderColor(Color.Blue)
            .Padding(1, 0);
        console.Write(headerPanel);

        console.WriteLine();

        // Configuration Grid
        var configGrid = new Grid()
            .AddColumn(new GridColumn().Width(16))
            .AddColumn();

        configGrid.AddRow("[grey]Template:[/]", $"[white]{EscapeMarkup(report.TemplatePath)}[/]");
        configGrid.AddRow("[grey]Subscription:[/]", $"[white]{EscapeMarkup(report.SubscriptionId ?? "N/A")}[/]");
        configGrid.AddRow("[grey]Resource Group:[/]", $"[white]{EscapeMarkup(report.ResourceGroup ?? "N/A")}[/]");
        configGrid.AddRow("[grey]Generated:[/]", $"[white]{report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC[/]");

        console.Write(configGrid);
        console.WriteLine();

        // Summary Table
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Summary[/]")
            .AddColumn(new TableColumn("[grey]Status[/]").Centered())
            .AddColumn(new TableColumn("[grey]Count[/]").Centered());

        summaryTable.AddRow(
            new Markup("[green]✓ In Sync[/]"),
            new Markup($"[green]{report.InSyncCount}[/]"));
        summaryTable.AddRow(
            new Markup("[red]✗ Drifted[/]"),
            new Markup($"[red]{report.DriftedCount}[/]"));
        summaryTable.AddRow(
            new Markup("[yellow]⚠ Missing[/]"),
            new Markup($"[yellow]{report.MissingCount}[/]"));
        summaryTable.AddRow(
            new Markup("[blue]? Unmanaged[/]"),
            new Markup($"[blue]{report.UnmanagedCount}[/]"));

        if (report.ErrorCount > 0)
        {
            summaryTable.AddRow(
                new Markup("[red]! Errors[/]"),
                new Markup($"[red]{report.ErrorCount}[/]"));
        }

        console.Write(summaryTable);
        console.WriteLine();

        // Detailed Results
        var driftedResults = report.Results.Where(r => r.Status != DriftStatus.InSync).ToList();

        if (driftedResults.Count > 0)
        {
            console.Write(new Rule("[bold]Drift Details[/]").RuleStyle("grey").LeftJustified());
            console.WriteLine();

            foreach (var result in driftedResults)
            {
                RenderResult(console, result);
            }
        }
        else
        {
            var successPanel = new Panel(
                new Markup("[green]✓ All resources are in sync with the template.[/]"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green)
                .Padding(1, 0);
            console.Write(successPanel);
        }

        console.WriteLine();
    }

    private static void RenderResult(IAnsiConsole console, DriftResult result)
    {
        var (statusColor, statusIcon, statusText) = result.Status switch
        {
            DriftStatus.Drifted => (Color.Red, "✗", "DRIFTED"),
            DriftStatus.Missing => (Color.Yellow, "⚠", "MISSING"),
            DriftStatus.Unmanaged => (Color.Blue, "?", "UNMANAGED"),
            DriftStatus.Error => (Color.Red, "!", "ERROR"),
            _ => (Color.White, "•", result.Status.ToString())
        };

        // Resource header
        var resourcePanel = new Panel(
            new Rows(
                new Markup($"[bold]{EscapeMarkup(result.ResourceName)}[/]"),
                new Markup($"[grey]{EscapeMarkup(result.ResourceType)}[/]")))
            .Header($"[{statusColor.ToMarkup()}]{statusIcon} {statusText}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(statusColor)
            .Padding(1, 0);

        console.Write(resourcePanel);

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            console.Write(new Markup($"  [red]Error: {EscapeMarkup(result.ErrorMessage)}[/]"));
            console.WriteLine();
        }

        if (result.Drifts.Count > 0)
        {
            var driftTable = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[grey]Type[/]").Width(8))
                .AddColumn(new TableColumn("[grey]Property[/]"))
                .AddColumn(new TableColumn("[grey]Expected[/]"))
                .AddColumn(new TableColumn("[grey]Actual[/]"));

            foreach (var drift in result.Drifts)
            {
                var driftIcon = drift.DriftType switch
                {
                    DriftType.Modified => "~",
                    DriftType.Missing => "-",
                    DriftType.Added => "+",
                    DriftType.TypeChanged => "!",
                    _ => "?"
                };

                var driftColor = drift.DriftType switch
                {
                    DriftType.Modified => Color.Yellow,
                    DriftType.Missing => Color.Red,
                    DriftType.Added => Color.Green,
                    DriftType.TypeChanged => Color.Fuchsia,
                    _ => Color.Grey
                };

                var expected = drift.ExpectedValue.HasValue
                    ? FormatValue(drift.ExpectedValue.Value)
                    : "[grey]—[/]";
                var actual = drift.ActualValue.HasValue
                    ? FormatValue(drift.ActualValue.Value)
                    : "[grey]—[/]";

                driftTable.AddRow(
                    new Markup($"[{driftColor.ToMarkup()}]{driftIcon} {drift.DriftType}[/]"),
                    new Markup($"[white]{EscapeMarkup(drift.PropertyPath)}[/]"),
                    new Markup($"[green]{expected}[/]"),
                    new Markup($"[red]{actual}[/]"));
            }

            console.Write(driftTable);
        }

        console.WriteLine();
    }

    private static string FormatValue(System.Text.Json.JsonElement element)
    {
        var raw = element.GetRawText();
        var escaped = EscapeMarkup(raw);
        return escaped.Length > 60 ? escaped[..57] + "..." : escaped;
    }

    private static string EscapeMarkup(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}
