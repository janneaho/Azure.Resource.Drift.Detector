using System.Text;
using System.Text.Json;
using AzureDriftDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Integrations;

/// <summary>
/// Slack webhook notification client.
/// </summary>
public sealed class SlackNotificationClient : INotificationClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly ILogger<SlackNotificationClient> _logger;

    public SlackNotificationClient(
        string webhookUrl,
        ILogger<SlackNotificationClient> logger,
        HttpClient? httpClient = null)
    {
        _webhookUrl = webhookUrl;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task SendNotificationAsync(
        DriftReport report,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildSlackPayload(report);
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Sent drift notification to Slack");
    }

    private static object BuildSlackPayload(DriftReport report)
    {
        var statusEmoji = report.HasDrift ? ":warning:" : ":white_check_mark:";
        var statusText = report.HasDrift ? "Drift Detected" : "No Drift Detected";
        var color = report.HasDrift ? "#ff0000" : "#36a64f";

        var fields = new List<object>
        {
            new { title = "Template", value = report.TemplatePath, @short = true },
            new { title = "Resource Group", value = report.ResourceGroup, @short = true },
            new { title = "In Sync", value = report.InSyncCount.ToString(), @short = true },
            new { title = "Drifted", value = report.DriftedCount.ToString(), @short = true }
        };

        if (report.MissingCount > 0)
        {
            fields.Add(new { title = "Missing", value = report.MissingCount.ToString(), @short = true });
        }

        if (report.UnmanagedCount > 0)
        {
            fields.Add(new { title = "Unmanaged", value = report.UnmanagedCount.ToString(), @short = true });
        }

        var driftedResources = report.Results
            .Where(r => r.Status == DriftStatus.Drifted)
            .Take(5)
            .Select(r => $"• {r.ResourceName} ({r.ResourceType})")
            .ToList();

        var text = driftedResources.Count > 0
            ? $"*Drifted Resources:*\n{string.Join("\n", driftedResources)}"
            : null;

        if (report.DriftedCount > 5)
        {
            text += $"\n_...and {report.DriftedCount - 5} more_";
        }

        return new
        {
            attachments = new[]
            {
                new
                {
                    fallback = $"Azure Drift Report: {statusText}",
                    color,
                    pretext = $"{statusEmoji} *Azure Resource Drift Report*",
                    text,
                    fields,
                    footer = "Azure Drift Detector",
                    ts = report.GeneratedAt.ToUnixTimeSeconds()
                }
            }
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
