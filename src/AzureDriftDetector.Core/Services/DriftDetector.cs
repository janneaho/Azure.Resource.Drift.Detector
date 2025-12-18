using System.Text.Json;
using AzureDriftDetector.Core.Configuration;
using AzureDriftDetector.Core.Models;
using AzureDriftDetector.Core.Parsing;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Services;

/// <summary>
/// Core drift detection engine.
/// </summary>
public sealed class DriftDetector : IDriftDetector
{
    private readonly TemplateParserFactory _parserFactory;
    private readonly IAzureResourceService _resourceService;
    private readonly DriftDetectorConfig _config;
    private readonly ILogger<DriftDetector> _logger;

    public DriftDetector(
        TemplateParserFactory parserFactory,
        IAzureResourceService resourceService,
        DriftDetectorConfig config,
        ILogger<DriftDetector> logger)
    {
        _parserFactory = parserFactory;
        _resourceService = resourceService;
        _config = config;
        _logger = logger;
    }

    public DriftResult Compare(ResourceState expected, ResourceState? actual)
    {
        if (actual == null)
        {
            return new DriftResult
            {
                ResourceId = expected.ResourceId,
                ResourceType = expected.ResourceType,
                ResourceName = expected.Name,
                Status = DriftStatus.Missing
            };
        }

        var drifts = new List<PropertyDrift>();

        // Compare location
        if (!string.IsNullOrEmpty(expected.Location) &&
            !string.Equals(expected.Location, actual.Location, StringComparison.OrdinalIgnoreCase))
        {
            drifts.Add(new PropertyDrift
            {
                PropertyPath = "location",
                ExpectedValue = JsonDocument.Parse($"\"{expected.Location}\"").RootElement,
                ActualValue = JsonDocument.Parse($"\"{actual.Location}\"").RootElement,
                DriftType = DriftType.Modified
            });
        }

        // Compare tags
        var tagDrifts = CompareTags(expected.Tags, actual.Tags);
        drifts.AddRange(tagDrifts);

        // Compare properties
        if (expected.Properties.ValueKind != JsonValueKind.Undefined)
        {
            var propertyDrifts = CompareJsonElements(
                expected.Properties,
                actual.Properties,
                "properties",
                expected.ResourceType,
                0);
            drifts.AddRange(propertyDrifts);
        }

        return new DriftResult
        {
            ResourceId = expected.ResourceId,
            ResourceType = expected.ResourceType,
            ResourceName = expected.Name,
            Status = drifts.Count > 0 ? DriftStatus.Drifted : DriftStatus.InSync,
            Drifts = drifts
        };
    }

    public async Task<DriftReport> GenerateReportAsync(
        string templatePath,
        string subscriptionId,
        string resourceGroup,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating drift report for template: {Template}",
            templatePath);

        var parser = _parserFactory.GetParser(templatePath);
        var expectedStates = await parser.ParseAsync(templatePath, parameters, cancellationToken);

        var actualResources = await _resourceService.GetResourceGroupResourcesAsync(
            subscriptionId,
            resourceGroup,
            cancellationToken);

        var actualByName = actualResources
            .GroupBy(r => (r.ResourceType, r.Name), StringOrdinalIgnoreCaseComparer.Instance)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringOrdinalIgnoreCaseComparer.Instance);

        var results = new List<DriftResult>();

        foreach (var expected in expectedStates)
        {
            var key = (expected.ResourceType, expected.Name);
            actualByName.TryGetValue(key, out var actual);
            actualByName.Remove(key);

            var result = Compare(expected, actual);
            results.Add(result);
        }

        // Add unmanaged resources (exist in Azure but not in template)
        foreach (var unmanaged in actualByName.Values)
        {
            results.Add(new DriftResult
            {
                ResourceId = unmanaged.ResourceId,
                ResourceType = unmanaged.ResourceType,
                ResourceName = unmanaged.Name,
                Status = DriftStatus.Unmanaged
            });
        }

        return new DriftReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            TemplatePath = templatePath,
            SubscriptionId = subscriptionId,
            ResourceGroup = resourceGroup,
            Results = results
        };
    }

    private List<PropertyDrift> CompareTags(
        Dictionary<string, string> expected,
        Dictionary<string, string> actual)
    {
        var drifts = new List<PropertyDrift>();

        foreach (var (key, expectedValue) in expected)
        {
            var path = $"tags.{key}";
            if (_config.IgnoreRules.ShouldIgnore(path))
                continue;

            if (!actual.TryGetValue(key, out var actualValue))
            {
                drifts.Add(new PropertyDrift
                {
                    PropertyPath = path,
                    ExpectedValue = JsonDocument.Parse($"\"{expectedValue}\"").RootElement,
                    ActualValue = null,
                    DriftType = DriftType.Missing
                });
            }
            else if (!string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
            {
                drifts.Add(new PropertyDrift
                {
                    PropertyPath = path,
                    ExpectedValue = JsonDocument.Parse($"\"{expectedValue}\"").RootElement,
                    ActualValue = JsonDocument.Parse($"\"{actualValue}\"").RootElement,
                    DriftType = DriftType.Modified
                });
            }
        }

        if (_config.ReportAddedProperties)
        {
            foreach (var (key, actualValue) in actual)
            {
                if (!expected.ContainsKey(key))
                {
                    var path = $"tags.{key}";
                    if (_config.IgnoreRules.ShouldIgnore(path))
                        continue;

                    drifts.Add(new PropertyDrift
                    {
                        PropertyPath = path,
                        ExpectedValue = null,
                        ActualValue = JsonDocument.Parse($"\"{actualValue}\"").RootElement,
                        DriftType = DriftType.Added
                    });
                }
            }
        }

        return drifts;
    }

    private List<PropertyDrift> CompareJsonElements(
        JsonElement expected,
        JsonElement actual,
        string path,
        string resourceType,
        int depth)
    {
        var drifts = new List<PropertyDrift>();

        if (depth >= _config.MaxPropertyDepth)
            return drifts;

        if (_config.IgnoreRules.ShouldIgnore(path, resourceType))
            return drifts;

        // Handle null/undefined cases
        var expectedIsEmpty = expected.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;
        var actualIsEmpty = actual.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

        if (expectedIsEmpty && actualIsEmpty)
            return drifts;

        if (expectedIsEmpty && !actualIsEmpty && _config.ReportAddedProperties)
        {
            drifts.Add(new PropertyDrift
            {
                PropertyPath = path,
                ExpectedValue = null,
                ActualValue = actual,
                DriftType = DriftType.Added
            });
            return drifts;
        }

        if (!expectedIsEmpty && actualIsEmpty)
        {
            drifts.Add(new PropertyDrift
            {
                PropertyPath = path,
                ExpectedValue = expected,
                ActualValue = null,
                DriftType = DriftType.Missing
            });
            return drifts;
        }

        // Type mismatch
        if (expected.ValueKind != actual.ValueKind)
        {
            drifts.Add(new PropertyDrift
            {
                PropertyPath = path,
                ExpectedValue = expected,
                ActualValue = actual,
                DriftType = DriftType.TypeChanged
            });
            return drifts;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                drifts.AddRange(CompareObjects(expected, actual, path, resourceType, depth));
                break;

            case JsonValueKind.Array:
                drifts.AddRange(CompareArrays(expected, actual, path, resourceType, depth));
                break;

            default:
                if (!JsonElementEquals(expected, actual))
                {
                    drifts.Add(new PropertyDrift
                    {
                        PropertyPath = path,
                        ExpectedValue = expected,
                        ActualValue = actual,
                        DriftType = DriftType.Modified
                    });
                }
                break;
        }

        return drifts;
    }

    private List<PropertyDrift> CompareObjects(
        JsonElement expected,
        JsonElement actual,
        string path,
        string resourceType,
        int depth)
    {
        var drifts = new List<PropertyDrift>();
        var actualProperties = actual.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var prop in expected.EnumerateObject())
        {
            var propPath = $"{path}.{prop.Name}";

            if (_config.IgnoreRules.ShouldIgnore(propPath, resourceType))
                continue;

            if (actualProperties.TryGetValue(prop.Name, out var actualValue))
            {
                actualProperties.Remove(prop.Name);
                drifts.AddRange(CompareJsonElements(
                    prop.Value,
                    actualValue,
                    propPath,
                    resourceType,
                    depth + 1));
            }
            else
            {
                drifts.Add(new PropertyDrift
                {
                    PropertyPath = propPath,
                    ExpectedValue = prop.Value,
                    ActualValue = null,
                    DriftType = DriftType.Missing
                });
            }
        }

        if (_config.ReportAddedProperties)
        {
            foreach (var (name, value) in actualProperties)
            {
                var propPath = $"{path}.{name}";
                if (_config.IgnoreRules.ShouldIgnore(propPath, resourceType))
                    continue;

                drifts.Add(new PropertyDrift
                {
                    PropertyPath = propPath,
                    ExpectedValue = null,
                    ActualValue = value,
                    DriftType = DriftType.Added
                });
            }
        }

        return drifts;
    }

    private List<PropertyDrift> CompareArrays(
        JsonElement expected,
        JsonElement actual,
        string path,
        string resourceType,
        int depth)
    {
        var drifts = new List<PropertyDrift>();
        var expectedArray = expected.EnumerateArray().ToList();
        var actualArray = actual.EnumerateArray().ToList();

        if (expectedArray.Count != actualArray.Count)
        {
            drifts.Add(new PropertyDrift
            {
                PropertyPath = $"{path}.length",
                ExpectedValue = JsonDocument.Parse($"{expectedArray.Count}").RootElement,
                ActualValue = JsonDocument.Parse($"{actualArray.Count}").RootElement,
                DriftType = DriftType.Modified
            });
        }

        var minLength = Math.Min(expectedArray.Count, actualArray.Count);
        for (var i = 0; i < minLength; i++)
        {
            drifts.AddRange(CompareJsonElements(
                expectedArray[i],
                actualArray[i],
                $"{path}[{i}]",
                resourceType,
                depth + 1));
        }

        return drifts;
    }

    private static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(
                a.GetString(),
                b.GetString(),
                StringComparison.Ordinal),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            _ => a.GetRawText() == b.GetRawText()
        };
    }

    private sealed class StringOrdinalIgnoreCaseComparer :
        IEqualityComparer<(string, string)>
    {
        public static readonly StringOrdinalIgnoreCaseComparer Instance = new();

        public bool Equals((string, string) x, (string, string) y)
        {
            return string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string, string) obj)
        {
            return HashCode.Combine(
                obj.Item1.ToUpperInvariant(),
                obj.Item2.ToUpperInvariant());
        }
    }
}
