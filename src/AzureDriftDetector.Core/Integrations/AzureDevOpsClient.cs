using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AzureDriftDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Integrations;

/// <summary>
/// Azure DevOps REST API client for PR integration.
/// </summary>
public sealed class AzureDevOpsClient : IAzureDevOpsClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsClient> _logger;
    private readonly string _accessToken;
    private const string ApiVersion = "7.1";

    public AzureDevOpsClient(
        string accessToken,
        ILogger<AzureDevOpsClient> logger,
        HttpClient? httpClient = null)
    {
        _accessToken = accessToken;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();

        var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_accessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", authHeader);
    }

    public async Task PostPullRequestCommentAsync(
        string organizationUrl,
        string project,
        int pullRequestId,
        DriftReport report,
        CancellationToken cancellationToken = default)
    {
        var comment = FormatReportAsComment(report);

        var url = $"{organizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(project)}/" +
                  $"_apis/git/repositories/{Uri.EscapeDataString(project)}/pullRequests/{pullRequestId}/threads" +
                  $"?api-version={ApiVersion}";

        var payload = new
        {
            comments = new[]
            {
                new
                {
                    parentCommentId = 0,
                    content = comment,
                    commentType = 1 // Text
                }
            },
            status = report.HasDrift ? 1 : 4 // Active : Fixed
        };

        await PostAsync(url, payload, cancellationToken);

        _logger.LogInformation(
            "Posted drift report comment to PR #{PullRequestId}",
            pullRequestId);
    }

    public async Task UpsertPullRequestCommentAsync(
        string organizationUrl,
        string project,
        int pullRequestId,
        DriftReport report,
        string commentIdentifier,
        CancellationToken cancellationToken = default)
    {
        var existingThread = await FindExistingThreadAsync(
            organizationUrl,
            project,
            pullRequestId,
            commentIdentifier,
            cancellationToken);

        var comment = FormatReportAsComment(report, commentIdentifier);

        if (existingThread != null)
        {
            await UpdateThreadCommentAsync(
                organizationUrl,
                project,
                pullRequestId,
                existingThread.Value.threadId,
                existingThread.Value.commentId,
                comment,
                cancellationToken);
        }
        else
        {
            var url = $"{organizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(project)}/" +
                      $"_apis/git/repositories/{Uri.EscapeDataString(project)}/pullRequests/{pullRequestId}/threads" +
                      $"?api-version={ApiVersion}";

            var payload = new
            {
                comments = new[]
                {
                    new
                    {
                        parentCommentId = 0,
                        content = comment,
                        commentType = 1
                    }
                },
                status = report.HasDrift ? 1 : 4
            };

            await PostAsync(url, payload, cancellationToken);
        }

        _logger.LogInformation(
            "Upserted drift report comment to PR #{PullRequestId}",
            pullRequestId);
    }

    private async Task<(int threadId, int commentId)?> FindExistingThreadAsync(
        string organizationUrl,
        string project,
        int pullRequestId,
        string identifier,
        CancellationToken cancellationToken)
    {
        var url = $"{organizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(project)}/" +
                  $"_apis/git/repositories/{Uri.EscapeDataString(project)}/pullRequests/{pullRequestId}/threads" +
                  $"?api-version={ApiVersion}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("value", out var threads))
        {
            foreach (var thread in threads.EnumerateArray())
            {
                if (thread.TryGetProperty("comments", out var comments))
                {
                    foreach (var comment in comments.EnumerateArray())
                    {
                        if (comment.TryGetProperty("content", out var contentProp) &&
                            contentProp.GetString()?.Contains(identifier) == true)
                        {
                            var threadId = thread.GetProperty("id").GetInt32();
                            var commentId = comment.GetProperty("id").GetInt32();
                            return (threadId, commentId);
                        }
                    }
                }
            }
        }

        return null;
    }

    private async Task UpdateThreadCommentAsync(
        string organizationUrl,
        string project,
        int pullRequestId,
        int threadId,
        int commentId,
        string content,
        CancellationToken cancellationToken)
    {
        var url = $"{organizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(project)}/" +
                  $"_apis/git/repositories/{Uri.EscapeDataString(project)}/pullRequests/{pullRequestId}/" +
                  $"threads/{threadId}/comments/{commentId}" +
                  $"?api-version={ApiVersion}";

        var payload = new { content };

        var json = JsonSerializer.Serialize(payload);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PatchAsync(url, httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task PostAsync(string url, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string FormatReportAsComment(DriftReport report, string? identifier = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(identifier))
        {
            sb.AppendLine($"<!-- {identifier} -->");
        }

        sb.AppendLine("## 🔍 Azure Resource Drift Report");
        sb.AppendLine();

        var statusEmoji = report.HasDrift ? "⚠️" : "✅";
        var statusText = report.HasDrift ? "Drift Detected" : "No Drift Detected";

        sb.AppendLine($"**Status:** {statusEmoji} {statusText}");
        sb.AppendLine();

        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Count |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| ✅ In Sync | {report.InSyncCount} |");
        sb.AppendLine($"| ❌ Drifted | {report.DriftedCount} |");
        sb.AppendLine($"| ⚠️ Missing | {report.MissingCount} |");
        sb.AppendLine($"| ❓ Unmanaged | {report.UnmanagedCount} |");
        sb.AppendLine();

        if (report.Results.Any(r => r.Status == DriftStatus.Drifted))
        {
            sb.AppendLine("### Drifted Resources");
            sb.AppendLine();

            foreach (var result in report.Results.Where(r => r.Status == DriftStatus.Drifted))
            {
                sb.AppendLine($"#### ❌ {result.ResourceName}");
                sb.AppendLine($"Type: `{result.ResourceType}`");
                sb.AppendLine();

                if (result.Drifts.Count > 0)
                {
                    sb.AppendLine("| Property | Expected | Actual |");
                    sb.AppendLine("|----------|----------|--------|");

                    foreach (var drift in result.Drifts.Take(10))
                    {
                        var expected = drift.ExpectedValue.HasValue
                            ? TruncateValue(drift.ExpectedValue.Value.GetRawText())
                            : "_not set_";
                        var actual = drift.ActualValue.HasValue
                            ? TruncateValue(drift.ActualValue.Value.GetRawText())
                            : "_not set_";

                        sb.AppendLine($"| `{drift.PropertyPath}` | {expected} | {actual} |");
                    }

                    if (result.Drifts.Count > 10)
                    {
                        sb.AppendLine($"| ... | _+{result.Drifts.Count - 10} more_ | |");
                    }

                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("---");
        sb.AppendLine($"_Generated by Azure Drift Detector at {report.GeneratedAt:u}_");

        return sb.ToString();
    }

    private static string TruncateValue(string value)
    {
        var escaped = value.Replace("|", "\\|").Replace("\n", " ");
        return escaped.Length > 40 ? $"`{escaped[..37]}...`" : $"`{escaped}`";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
