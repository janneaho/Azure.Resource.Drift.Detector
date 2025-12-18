using System.Text;
using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Cli.Output;

public sealed class MarkdownReportFormatter : IReportFormatter
{
    public string Format(DriftReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Azure Resource Drift Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        sb.AppendLine("## Configuration");
        sb.AppendLine();
        sb.AppendLine($"- **Template:** `{report.TemplatePath}`");
        sb.AppendLine($"- **Subscription:** `{report.SubscriptionId}`");
        sb.AppendLine($"- **Resource Group:** `{report.ResourceGroup}`");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Status | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| :white_check_mark: In Sync | {report.InSyncCount} |");
        sb.AppendLine($"| :x: Drifted | {report.DriftedCount} |");
        sb.AppendLine($"| :warning: Missing | {report.MissingCount} |");
        sb.AppendLine($"| :question: Unmanaged | {report.UnmanagedCount} |");

        if (report.ErrorCount > 0)
        {
            sb.AppendLine($"| :exclamation: Errors | {report.ErrorCount} |");
        }

        sb.AppendLine();

        if (report.Results.Any(r => r.Status != DriftStatus.InSync))
        {
            sb.AppendLine("## Drift Details");
            sb.AppendLine();

            foreach (var result in report.Results.Where(r => r.Status != DriftStatus.InSync))
            {
                FormatResult(sb, result);
            }
        }
        else
        {
            sb.AppendLine("> :tada: All resources are in sync with the template!");
        }

        return sb.ToString();
    }

    private static void FormatResult(StringBuilder sb, DriftResult result)
    {
        var statusEmoji = result.Status switch
        {
            DriftStatus.Drifted => ":x:",
            DriftStatus.Missing => ":warning:",
            DriftStatus.Unmanaged => ":question:",
            DriftStatus.Error => ":exclamation:",
            _ => ":white_check_mark:"
        };

        sb.AppendLine($"### {statusEmoji} {result.ResourceName}");
        sb.AppendLine();
        sb.AppendLine($"- **Type:** `{result.ResourceType}`");
        sb.AppendLine($"- **Status:** {result.Status}");

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            sb.AppendLine($"- **Error:** {result.ErrorMessage}");
        }

        sb.AppendLine();

        if (result.Drifts.Count > 0)
        {
            sb.AppendLine("| Property | Expected | Actual | Type |");
            sb.AppendLine("|----------|----------|--------|------|");

            foreach (var drift in result.Drifts)
            {
                var expected = drift.ExpectedValue.HasValue
                    ? FormatValue(drift.ExpectedValue.Value)
                    : "_not set_";
                var actual = drift.ActualValue.HasValue
                    ? FormatValue(drift.ActualValue.Value)
                    : "_not set_";

                sb.AppendLine(
                    $"| `{drift.PropertyPath}` | {expected} | {actual} | {drift.DriftType} |");
            }

            sb.AppendLine();
        }
    }

    private static string FormatValue(System.Text.Json.JsonElement element)
    {
        var raw = element.GetRawText();
        var escaped = raw.Replace("|", "\\|").Replace("\n", " ");
        return escaped.Length > 50 ? $"`{escaped[..47]}...`" : $"`{escaped}`";
    }
}
