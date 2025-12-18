using System.Text.Json;

namespace AzureDriftDetector.Core.Models;

/// <summary>
/// Represents the state of an Azure resource at a point in time.
/// </summary>
public sealed record ResourceState
{
    public required string ResourceId { get; init; }
    public required string ResourceType { get; init; }
    public required string Name { get; init; }
    public string? ResourceGroup { get; init; }
    public string? Location { get; init; }
    public Dictionary<string, string> Tags { get; init; } = [];
    public JsonElement Properties { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>
/// Source of the resource state.
/// </summary>
public enum ResourceStateSource
{
    Template,
    Azure
}
