using AzureDriftDetector.Cli.Commands;

namespace AzureDriftDetector.Cli.Output;

public static class ReportFormatterFactory
{
    public static IReportFormatter Create(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Console => new ConsoleReportFormatter(),
            OutputFormat.Json => new JsonReportFormatter(),
            OutputFormat.Markdown => new MarkdownReportFormatter(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown output format")
        };
    }
}
