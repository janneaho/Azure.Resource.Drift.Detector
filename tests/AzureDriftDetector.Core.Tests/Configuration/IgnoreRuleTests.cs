using AzureDriftDetector.Core.Configuration;
using FluentAssertions;

namespace AzureDriftDetector.Core.Tests.Configuration;

public class IgnoreRuleTests
{
    [Fact]
    public void Matches_WithExactPattern_ReturnsTrue()
    {
        var rule = new IgnoreRule
        {
            Pattern = "properties.provisioningState"
        };

        rule.Matches("properties.provisioningState").Should().BeTrue();
    }

    [Fact]
    public void Matches_WithDifferentPath_ReturnsFalse()
    {
        var rule = new IgnoreRule
        {
            Pattern = "properties.provisioningState"
        };

        rule.Matches("properties.location").Should().BeFalse();
    }

    [Fact]
    public void Matches_WithSingleWildcard_MatchesSingleLevel()
    {
        var rule = new IgnoreRule
        {
            Pattern = "properties.*.id"
        };

        rule.Matches("properties.network.id").Should().BeTrue();
        rule.Matches("properties.storage.id").Should().BeTrue();
        rule.Matches("properties.network.subnet.id").Should().BeFalse();
    }

    [Fact]
    public void Matches_WithDoubleWildcard_MatchesMultipleLevels()
    {
        // ** matches any characters including dots (multi-level)
        var rule = new IgnoreRule
        {
            Pattern = "properties.**"
        };

        rule.Matches("properties.resourceId").Should().BeTrue();
        rule.Matches("properties.network.resourceId").Should().BeTrue();
        rule.Matches("properties.network.config.nicId").Should().BeTrue();
    }

    [Fact]
    public void Matches_WithResourceTypeFilter_OnlyMatchesType()
    {
        var rule = new IgnoreRule
        {
            Pattern = "properties.siteConfig",
            ResourceType = "Microsoft.Web/sites"
        };

        rule.Matches("properties.siteConfig", "Microsoft.Web/sites").Should().BeTrue();
        rule.Matches("properties.siteConfig", "Microsoft.Storage/storageAccounts").Should().BeFalse();
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var rule = new IgnoreRule
        {
            Pattern = "Properties.ProvisioningState"
        };

        rule.Matches("properties.provisioningstate").Should().BeTrue();
        rule.Matches("PROPERTIES.PROVISIONINGSTATE").Should().BeTrue();
    }
}

public class IgnoreRuleSetTests
{
    [Fact]
    public void CreateDefault_IncludesCommonIgnoreRules()
    {
        var ruleSet = IgnoreRuleSet.CreateDefault();

        ruleSet.ShouldIgnore("properties.provisioningState").Should().BeTrue();
        ruleSet.ShouldIgnore("properties.createdTime").Should().BeTrue();
        ruleSet.ShouldIgnore("properties.resourceGuid").Should().BeTrue();
    }

    [Fact]
    public void ShouldIgnore_WithAddedRule_ReturnsTrue()
    {
        var ruleSet = new IgnoreRuleSet();
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.customProperty"
        });

        ruleSet.ShouldIgnore("properties.customProperty").Should().BeTrue();
    }

    [Fact]
    public void ShouldIgnore_WithNoMatchingRule_ReturnsFalse()
    {
        var ruleSet = new IgnoreRuleSet();

        ruleSet.ShouldIgnore("properties.someProperty").Should().BeFalse();
    }

    [Fact]
    public void ShouldIgnore_WithResourceType_FiltersCorrectly()
    {
        var ruleSet = new IgnoreRuleSet();
        ruleSet.AddRule(new IgnoreRule
        {
            Pattern = "properties.setting",
            ResourceType = "Microsoft.Web/sites"
        });

        ruleSet.ShouldIgnore("properties.setting", "Microsoft.Web/sites").Should().BeTrue();
        ruleSet.ShouldIgnore("properties.setting", "Microsoft.Storage/storageAccounts").Should().BeFalse();
    }
}
