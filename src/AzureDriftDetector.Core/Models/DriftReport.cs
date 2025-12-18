namespace AzureDriftDetector.Core.Models;

/// <summary>
/// Complete drift detection report for a set of resources.
/// </summary>
public sealed record DriftReport
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string TemplatePath { get; init; }
    public string? SubscriptionId { get; init; }
    public string? ResourceGroup { get; init; }
    public IReadOnlyList<DriftResult> Results { get; init; } = [];

    public int TotalResources => Results.Count;
    public int InSyncCount => Results.Count(r => r.Status == DriftStatus.InSync);
    public int DriftedCount => Results.Count(r => r.Status == DriftStatus.Drifted);
    public int MissingCount => Results.Count(r => r.Status == DriftStatus.Missing);
    public int UnmanagedCount => Results.Count(r => r.Status == DriftStatus.Unmanaged);
    public int ErrorCount => Results.Count(r => r.Status == DriftStatus.Error);

    public bool HasDrift => DriftedCount > 0 || MissingCount > 0;
}
