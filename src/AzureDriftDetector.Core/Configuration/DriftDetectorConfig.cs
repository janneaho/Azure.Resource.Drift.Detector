namespace AzureDriftDetector.Core.Configuration;

/// <summary>
/// Configuration for drift detection behavior.
/// </summary>
public sealed class DriftDetectorConfig
{
    public IgnoreRuleSet IgnoreRules { get; init; } = IgnoreRuleSet.CreateDefault();

    /// <summary>
    /// Include properties that exist in Azure but not in template.
    /// </summary>
    public bool ReportAddedProperties { get; init; } = true;

    /// <summary>
    /// Maximum depth to traverse in property comparison.
    /// </summary>
    public int MaxPropertyDepth { get; init; } = 10;

    /// <summary>
    /// Treat null and missing properties as equivalent.
    /// </summary>
    public bool TreatNullAsMissing { get; init; } = true;
}
