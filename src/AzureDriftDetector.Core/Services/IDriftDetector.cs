using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Core.Services;

/// <summary>
/// Service for detecting drift between expected and actual resource state.
/// </summary>
public interface IDriftDetector
{
    /// <summary>
    /// Compares expected state (from template) with actual state (from Azure).
    /// </summary>
    DriftResult Compare(ResourceState expected, ResourceState? actual);

    /// <summary>
    /// Generates a complete drift report for a set of resources.
    /// </summary>
    Task<DriftReport> GenerateReportAsync(
        string templatePath,
        string subscriptionId,
        string resourceGroup,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);
}
