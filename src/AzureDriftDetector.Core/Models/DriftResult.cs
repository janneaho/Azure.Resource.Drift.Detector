namespace AzureDriftDetector.Core.Models;

/// <summary>
/// Represents drift detection result for a single Azure resource.
/// </summary>
public sealed record DriftResult
{
    public required string ResourceId { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceName { get; init; }
    public required DriftStatus Status { get; init; }
    public IReadOnlyList<PropertyDrift> Drifts { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public bool HasDrift => Status == DriftStatus.Drifted;
}

/// <summary>
/// Overall status of drift detection for a resource.
/// </summary>
public enum DriftStatus
{
    /// <summary>Resource matches expected state.</summary>
    InSync,

    /// <summary>Resource has configuration drift.</summary>
    Drifted,

    /// <summary>Resource exists in template but not in Azure.</summary>
    Missing,

    /// <summary>Resource exists in Azure but not in template.</summary>
    Unmanaged,

    /// <summary>Could not determine drift status due to an error.</summary>
    Error
}
