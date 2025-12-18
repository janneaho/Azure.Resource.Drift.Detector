using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Core.Integrations;

/// <summary>
/// Client for Azure DevOps integration.
/// </summary>
public interface IAzureDevOpsClient
{
    /// <summary>
    /// Posts a drift report as a comment on a pull request.
    /// </summary>
    Task PostPullRequestCommentAsync(
        string organizationUrl,
        string project,
        int pullRequestId,
        DriftReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing comment or creates a new one.
    /// </summary>
    Task UpsertPullRequestCommentAsync(
        string organizationUrl,
        string project,
        int pullRequestId,
        DriftReport report,
        string commentIdentifier,
        CancellationToken cancellationToken = default);
}
