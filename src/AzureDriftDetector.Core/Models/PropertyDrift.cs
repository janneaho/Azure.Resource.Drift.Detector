using System.Text.Json;

namespace AzureDriftDetector.Core.Models;

/// <summary>
/// Represents a single property difference between expected and actual state.
/// </summary>
public sealed record PropertyDrift
{
    public required string PropertyPath { get; init; }
    public JsonElement? ExpectedValue { get; init; }
    public JsonElement? ActualValue { get; init; }
    public required DriftType DriftType { get; init; }
}

/// <summary>
/// Type of drift detected for a property.
/// </summary>
public enum DriftType
{
    /// <summary>Property value was modified from expected.</summary>
    Modified,

    /// <summary>Property exists in template but missing in Azure.</summary>
    Missing,

    /// <summary>Property exists in Azure but not defined in template.</summary>
    Added,

    /// <summary>Property type changed between template and Azure.</summary>
    TypeChanged
}
