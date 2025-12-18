using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Core.Integrations;

/// <summary>
/// Interface for sending drift notifications to messaging platforms.
/// </summary>
public interface INotificationClient
{
    /// <summary>
    /// Sends a drift alert notification.
    /// </summary>
    Task SendNotificationAsync(
        DriftReport report,
        CancellationToken cancellationToken = default);
}
