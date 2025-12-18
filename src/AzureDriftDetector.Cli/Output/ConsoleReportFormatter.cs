using System.Text;
using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Cli.Output;

public sealed class ConsoleReportFormatter : IReportFormatter
{
    private const string Red = "\u001b[31m";
    private const string Green = "\u001b[32m";
    private const string Yellow = "\u001b[33m";
    private const string Blue = "\u001b[34m";
    private const string Reset = "\u001b[0m";
    private const string Bold = "\u001b[1m";

    public string Format(DriftReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine($"{Bold}Azure Resource Drift Report{Reset}");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine();

        sb.AppendLine($"Template:      {report.TemplatePath}");
        sb.AppendLine($"Subscription:  {report.SubscriptionId}");
        sb.AppendLine($"Resource Group:{report.ResourceGroup}");
        sb.AppendLine($"Generated:     {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary
        sb.AppendLine($"{Bold}Summary{Reset}");
        sb.AppendLine(new string('─', 30));
        sb.AppendLine($"Total Resources: {report.TotalResources}");
        sb.AppendLine($"  {Green}✓ In Sync:   {report.InSyncCount}{Reset}");
        sb.AppendLine($"  {Red}✗ Drifted:   {report.DriftedCount}{Reset}");
        sb.AppendLine($"  {Yellow}⚠ Missing:   {report.MissingCount}{Reset}");
        sb.AppendLine($"  {Blue}? Unmanaged: {report.UnmanagedCount}{Reset}");

        if (report.ErrorCount > 0)
        {
            sb.AppendLine($"  {Red}! Errors:    {report.ErrorCount}{Reset}");
        }

        sb.AppendLine();

        // Detailed results
        if (report.Results.Any(r => r.Status != DriftStatus.InSync))
        {
            sb.AppendLine($"{Bold}Drift Details{Reset}");
            sb.AppendLine(new string('─', 30));
            sb.AppendLine();

            foreach (var result in report.Results.Where(r => r.Status != DriftStatus.InSync))
            {
                FormatResult(sb, result);
            }
        }
        else
        {
            sb.AppendLine($"{Green}All resources are in sync with the template.{Reset}");
        }

        return sb.ToString();
    }

    private static void FormatResult(StringBuilder sb, DriftResult result)
    {
        var statusIcon = result.Status switch
        {
            DriftStatus.Drifted => $"{Red}✗{Reset}",
            DriftStatus.Missing => $"{Yellow}⚠{Reset}",
            DriftStatus.Unmanaged => $"{Blue}?{Reset}",
            DriftStatus.Error => $"{Red}!{Reset}",
            _ => "•"
        };

        var statusText = result.Status switch
        {
            DriftStatus.Drifted => $"{Red}DRIFTED{Reset}",
            DriftStatus.Missing => $"{Yellow}MISSING{Reset}",
            DriftStatus.Unmanaged => $"{Blue}UNMANAGED{Reset}",
            DriftStatus.Error => $"{Red}ERROR{Reset}",
            _ => result.Status.ToString()
        };

        sb.AppendLine($"{statusIcon} {Bold}{result.ResourceName}{Reset} ({result.ResourceType})");
        sb.AppendLine($"  Status: {statusText}");

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            sb.AppendLine($"  Error: {result.ErrorMessage}");
        }

        foreach (var drift in result.Drifts)
        {
            FormatDrift(sb, drift);
        }

        sb.AppendLine();
    }

    private static void FormatDrift(StringBuilder sb, PropertyDrift drift)
    {
        var driftIcon = drift.DriftType switch
        {
            DriftType.Modified => "~",
            DriftType.Missing => "-",
            DriftType.Added => "+",
            DriftType.TypeChanged => "!",
            _ => "?"
        };

        sb.AppendLine($"  {driftIcon} {drift.PropertyPath}");

        if (drift.ExpectedValue.HasValue)
        {
            var expected = FormatValue(drift.ExpectedValue.Value);
            sb.AppendLine($"    Expected: {Green}{expected}{Reset}");
        }

        if (drift.ActualValue.HasValue)
        {
            var actual = FormatValue(drift.ActualValue.Value);
            sb.AppendLine($"    Actual:   {Red}{actual}{Reset}");
        }
    }

    private static string FormatValue(System.Text.Json.JsonElement element)
    {
        var raw = element.GetRawText();
        return raw.Length > 100 ? raw[..97] + "..." : raw;
    }
}
