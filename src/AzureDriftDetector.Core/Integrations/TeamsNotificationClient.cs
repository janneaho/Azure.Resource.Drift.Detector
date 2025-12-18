using System.Text;
using System.Text.Json;
using AzureDriftDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Integrations;

/// <summary>
/// Microsoft Teams webhook notification client (Adaptive Cards).
/// </summary>
public sealed class TeamsNotificationClient : INotificationClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly ILogger<TeamsNotificationClient> _logger;

    public TeamsNotificationClient(
        string webhookUrl,
        ILogger<TeamsNotificationClient> logger,
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
        var payload = BuildTeamsPayload(report);
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Sent drift notification to Microsoft Teams");
    }

    private static object BuildTeamsPayload(DriftReport report)
    {
        var statusColor = report.HasDrift ? "attention" : "good";
        var statusText = report.HasDrift ? "⚠️ Drift Detected" : "✅ No Drift Detected";

        var facts = new List<object>
        {
            new { title = "Template", value = report.TemplatePath },
            new { title = "Resource Group", value = report.ResourceGroup ?? "N/A" },
            new { title = "Subscription", value = report.SubscriptionId ?? "N/A" },
            new { title = "Total Resources", value = report.TotalResources.ToString() }
        };

        var summaryColumns = new List<object>
        {
            new
            {
                type = "Column",
                width = "auto",
                items = new object[]
                {
                    new { type = "TextBlock", text = "✅ In Sync", weight = "bolder" },
                    new { type = "TextBlock", text = report.InSyncCount.ToString(), size = "extraLarge", color = "good" }
                }
            },
            new
            {
                type = "Column",
                width = "auto",
                items = new object[]
                {
                    new { type = "TextBlock", text = "❌ Drifted", weight = "bolder" },
                    new { type = "TextBlock", text = report.DriftedCount.ToString(), size = "extraLarge", color = "attention" }
                }
            },
            new
            {
                type = "Column",
                width = "auto",
                items = new object[]
                {
                    new { type = "TextBlock", text = "⚠️ Missing", weight = "bolder" },
                    new { type = "TextBlock", text = report.MissingCount.ToString(), size = "extraLarge", color = "warning" }
                }
            }
        };

        var bodyItems = new List<object>
        {
            new
            {
                type = "TextBlock",
                size = "large",
                weight = "bolder",
                text = "Azure Resource Drift Report"
            },
            new
            {
                type = "TextBlock",
                text = statusText,
                color = statusColor,
                weight = "bolder"
            },
            new { type = "FactSet", facts },
            new { type = "ColumnSet", columns = summaryColumns }
        };

        if (report.DriftedCount > 0)
        {
            bodyItems.Add(new
            {
                type = "TextBlock",
                text = "Drifted Resources:",
                weight = "bolder",
                separator = true
            });

            foreach (var result in report.Results.Where(r => r.Status == DriftStatus.Drifted).Take(5))
            {
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"• {result.ResourceName} ({result.ResourceType})",
                    wrap = true
                });
            }

            if (report.DriftedCount > 5)
            {
                bodyItems.Add(new
                {
                    type = "TextBlock",
                    text = $"...and {report.DriftedCount - 5} more",
                    isSubtle = true
                });
            }
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = bodyItems
                    }
                }
            }
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
