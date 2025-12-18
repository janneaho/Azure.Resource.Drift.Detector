using System.Text.RegularExpressions;

namespace AzureDriftDetector.Core.Configuration;

/// <summary>
/// Rule for ignoring specific properties during drift detection.
/// </summary>
public sealed class IgnoreRule
{
    public required string Pattern { get; init; }
    public string? ResourceType { get; init; }
    public string? Reason { get; init; }

    private Regex? _compiledPattern;

    public bool Matches(string propertyPath, string? resourceType = null)
    {
        // Check resource type filter if specified
        if (!string.IsNullOrEmpty(ResourceType) &&
            !string.Equals(ResourceType, resourceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _compiledPattern ??= new Regex(
            WildcardToRegex(Pattern),
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        return _compiledPattern.IsMatch(propertyPath);
    }

    private static string WildcardToRegex(string pattern)
    {
        return "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^.]*")
            .Replace("\\?", ".") + "$";
    }
}

/// <summary>
/// Collection of ignore rules with default system-managed properties.
/// </summary>
public sealed class IgnoreRuleSet
{
    private readonly List<IgnoreRule> _rules = [];

    public IReadOnlyList<IgnoreRule> Rules => _rules;

    public static IgnoreRuleSet CreateDefault()
    {
        var ruleSet = new IgnoreRuleSet();

        // System-managed properties that commonly change
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.provisioningState",
            Reason = "Azure-managed provisioning state"
        });
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.createdTime",
            Reason = "Timestamp managed by Azure"
        });
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.lastModifiedTime",
            Reason = "Timestamp managed by Azure"
        });
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.uniqueId",
            Reason = "Azure-generated unique identifier"
        });
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.resourceGuid",
            Reason = "Azure-generated GUID"
        });
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.etag",
            Reason = "Azure ETag for concurrency"
        });
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.**.*Id",
            Reason = "Azure-generated IDs"
        });

        return ruleSet;
    }

    public void AddRule(IgnoreRule rule)
    {
        _rules.Add(rule);
    }

    public bool ShouldIgnore(string propertyPath, string? resourceType = null)
    {
        return _rules.Any(r => r.Matches(propertyPath, resourceType));
    }
}
