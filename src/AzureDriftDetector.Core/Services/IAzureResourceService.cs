using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Core.Services;

/// <summary>
/// Service for querying Azure resource state.
/// </summary>
public interface IAzureResourceService
{
    /// <summary>
    /// Gets the current state of a resource from Azure.
    /// </summary>
    Task<ResourceState?> GetResourceStateAsync(
        string resourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resources in a resource group.
    /// </summary>
    Task<IReadOnlyList<ResourceState>> GetResourceGroupResourcesAsync(
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resources by type in a subscription.
    /// </summary>
    Task<IReadOnlyList<ResourceState>> GetResourcesByTypeAsync(
        string subscriptionId,
        string resourceType,
        CancellationToken cancellationToken = default);
}
