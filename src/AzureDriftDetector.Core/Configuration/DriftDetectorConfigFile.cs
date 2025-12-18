using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Configuration;

/// <summary>
/// Configuration file model for drift detector settings.
/// </summary>
public sealed class DriftDetectorConfigFile
{
    private const string DefaultFileName = ".driftdetector.json";

    [JsonPropertyName("ignoreRules")]
    public List<IgnoreRuleConfig>? IgnoreRules { get; set; }

    [JsonPropertyName("reportAddedProperties")]
    public bool? ReportAddedProperties { get; set; }

    [JsonPropertyName("maxPropertyDepth")]
    public int? MaxPropertyDepth { get; set; }

    [JsonPropertyName("treatNullAsMissing")]
    public bool? TreatNullAsMissing { get; set; }

    public static DriftDetectorConfig LoadFromDirectory(
        string directory,
        ILogger? logger = null)
    {
        var filePath = Path.Combine(directory, DefaultFileName);

        if (!File.Exists(filePath))
        {
            logger?.LogDebug("No config file found at {Path}, using defaults", filePath);
            return new DriftDetectorConfig();
        }

        return LoadFromFile(filePath, logger);
    }

    public static DriftDetectorConfig LoadFromFile(
        string filePath,
        ILogger? logger = null)
    {
        logger?.LogDebug("Loading configuration from {Path}", filePath);

        var json = File.ReadAllText(filePath);
        var configFile = JsonSerializer.Deserialize<DriftDetectorConfigFile>(json, JsonOptions);

        if (configFile == null)
        {
            return new DriftDetectorConfig();
        }

        var ruleSet = IgnoreRuleSet.CreateDefault();

        if (configFile.IgnoreRules != null)
        {
            foreach (var ruleConfig in configFile.IgnoreRules)
            {
                ruleSet.AddRule(new IgnoreRule
                {
                    Pattern = ruleConfig.Pattern,
                    ResourceType = ruleConfig.ResourceType,
                    Reason = ruleConfig.Reason
                });
            }
        }

        return new DriftDetectorConfig
        {
            IgnoreRules = ruleSet,
            ReportAddedProperties = configFile.ReportAddedProperties ?? true,
            MaxPropertyDepth = configFile.MaxPropertyDepth ?? 10,
            TreatNullAsMissing = configFile.TreatNullAsMissing ?? true
        };
    }

    public static void SaveSampleConfig(string directory)
    {
        var sample = new DriftDetectorConfigFile
        {
            IgnoreRules =
            [
                new IgnoreRuleConfig
                {
                    Pattern = "properties.provisioningState",
                    Reason = "Azure-managed property"
                },
                new IgnoreRuleConfig
                {
                    Pattern = "properties.**.*Id",
                    Reason = "Azure-generated IDs"
                },
                new IgnoreRuleConfig
                {
                    Pattern = "tags.Environment",
                    ResourceType = "Microsoft.Web/sites",
                    Reason = "Ignore environment tag on App Services"
                }
            ],
            ReportAddedProperties = true,
            MaxPropertyDepth = 10,
            TreatNullAsMissing = true
        };

        var filePath = Path.Combine(directory, DefaultFileName);
        var json = JsonSerializer.Serialize(sample, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class IgnoreRuleConfig
{
    [JsonPropertyName("pattern")]
    public required string Pattern { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
