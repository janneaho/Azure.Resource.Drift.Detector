using System.Text.Json;
using AzureDriftDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Parsing;

/// <summary>
/// Parses ARM (Azure Resource Manager) JSON templates.
/// </summary>
public sealed class ArmTemplateParser : ITemplateParser
{
    private readonly ILogger<ArmTemplateParser> _logger;
    private static readonly string[] SupportedExtensions = [".json"];

    public ArmTemplateParser(ILogger<ArmTemplateParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<IReadOnlyList<ResourceState>> ParseAsync(
        string filePath,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template file not found: {filePath}", filePath);
        }

        _logger.LogDebug("Parsing ARM template: {FilePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var document = JsonDocument.Parse(content);

        if (!IsArmTemplate(document))
        {
            throw new InvalidOperationException($"File is not a valid ARM template: {filePath}");
        }

        var resources = new List<ResourceState>();
        var resolvedParameters = ResolveParameters(document, parameters);

        if (document.RootElement.TryGetProperty("resources", out var resourcesElement))
        {
            foreach (var resource in resourcesElement.EnumerateArray())
            {
                var state = ParseResource(resource, resolvedParameters);
                if (state != null)
                {
                    resources.Add(state);
                }
            }
        }

        _logger.LogInformation("Parsed {Count} resources from ARM template", resources.Count);
        return resources;
    }

    private static bool IsArmTemplate(JsonDocument document)
    {
        return document.RootElement.TryGetProperty("$schema", out var schema) &&
               schema.GetString()?.Contains("deploymentTemplate", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Dictionary<string, JsonElement> ResolveParameters(
        JsonDocument document,
        IDictionary<string, string>? providedParameters)
    {
        var resolved = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.TryGetProperty("parameters", out var parametersElement))
        {
            foreach (var param in parametersElement.EnumerateObject())
            {
                if (providedParameters?.TryGetValue(param.Name, out var providedValue) == true)
                {
                    resolved[param.Name] = JsonDocument.Parse($"\"{providedValue}\"").RootElement;
                }
                else if (param.Value.TryGetProperty("defaultValue", out var defaultValue))
                {
                    resolved[param.Name] = defaultValue.Clone();
                }
            }
        }

        return resolved;
    }

    private ResourceState? ParseResource(
        JsonElement resource,
        Dictionary<string, JsonElement> parameters)
    {
        if (!resource.TryGetProperty("type", out var typeElement) ||
            !resource.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var type = typeElement.GetString();
        var name = ResolveExpression(nameElement.GetString() ?? "", parameters);

        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(name))
        {
            return null;
        }

        string? location = null;
        if (resource.TryGetProperty("location", out var locationElement))
        {
            location = ResolveExpression(locationElement.GetString() ?? "", parameters);
        }

        var tags = new Dictionary<string, string>();
        if (resource.TryGetProperty("tags", out var tagsElement))
        {
            foreach (var tag in tagsElement.EnumerateObject())
            {
                var value = ResolveExpression(tag.Value.GetString() ?? "", parameters);
                tags[tag.Name] = value;
            }
        }

        JsonElement properties = default;
        if (resource.TryGetProperty("properties", out var propsElement))
        {
            properties = propsElement.Clone();
        }

        return new ResourceState
        {
            ResourceId = $"/providers/{type}/{name}",
            ResourceType = type,
            Name = name,
            Location = location,
            Tags = tags,
            Properties = properties
        };
    }

    private static string ResolveExpression(string value, Dictionary<string, JsonElement> parameters)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Handle [parameters('paramName')] expressions
        if (value.StartsWith("[parameters('") && value.EndsWith("')]"))
        {
            var paramName = value[13..^3];
            if (parameters.TryGetValue(paramName, out var paramValue))
            {
                return paramValue.ValueKind == JsonValueKind.String
                    ? paramValue.GetString() ?? value
                    : paramValue.GetRawText();
            }
        }

        // Handle [concat(...)] - basic support
        if (value.StartsWith("[concat(") && value.EndsWith(")]"))
        {
            // This is a simplified implementation
            // Full ARM expression evaluation would require a complete expression parser
            return value;
        }

        return value;
    }
}
