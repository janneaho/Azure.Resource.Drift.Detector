using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using AzureDriftDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Services;

/// <summary>
/// Azure Resource Graph based implementation for querying resource state.
/// </summary>
public sealed class AzureResourceGraphService : IAzureResourceService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureResourceGraphService> _logger;

    public AzureResourceGraphService(
        ArmClient armClient,
        ILogger<AzureResourceGraphService> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    public async Task<ResourceState?> GetResourceStateAsync(
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceId);

        _logger.LogDebug("Querying resource: {ResourceId}", resourceId);

        var query = $@"
            Resources
            | where id =~ '{EscapeQueryString(resourceId)}'
            | project id, name, type, location, resourceGroup, tags, properties
        ";

        var results = await ExecuteQueryAsync(query, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<ResourceState>> GetResourceGroupResourcesAsync(
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);
        ArgumentException.ThrowIfNullOrEmpty(resourceGroupName);

        _logger.LogDebug(
            "Querying resources in resource group: {ResourceGroup}",
            resourceGroupName);

        var query = $@"
            Resources
            | where subscriptionId =~ '{EscapeQueryString(subscriptionId)}'
            | where resourceGroup =~ '{EscapeQueryString(resourceGroupName)}'
            | project id, name, type, location, resourceGroup, tags, properties
        ";

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceState>> GetResourcesByTypeAsync(
        string subscriptionId,
        string resourceType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);
        ArgumentException.ThrowIfNullOrEmpty(resourceType);

        _logger.LogDebug("Querying resources of type: {ResourceType}", resourceType);

        var query = $@"
            Resources
            | where subscriptionId =~ '{EscapeQueryString(subscriptionId)}'
            | where type =~ '{EscapeQueryString(resourceType)}'
            | project id, name, type, location, resourceGroup, tags, properties
        ";

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    private async Task<IReadOnlyList<ResourceState>> ExecuteQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var tenant = _armClient.GetTenants().First();

        var content = new ResourceQueryContent(query)
        {
            Options = new ResourceQueryRequestOptions
            {
                ResultFormat = ResultFormat.ObjectArray
            }
        };

        var results = new List<ResourceState>();
        string? skipToken = null;

        do
        {
            if (skipToken != null)
            {
                content.Options.SkipToken = skipToken;
            }

            Response<ResourceQueryResult> response;
            try
            {
                response = await tenant.GetResourcesAsync(content, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Resource Graph query failed: {Message}", ex.Message);
                throw;
            }

            var result = response.Value;
            skipToken = result.SkipToken;

            if (result.Data != null)
            {
                using var doc = JsonDocument.Parse(result.Data.ToString());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var state = ParseResourceFromQuery(item);
                        if (state != null)
                        {
                            results.Add(state);
                        }
                    }
                }
            }

        } while (!string.IsNullOrEmpty(skipToken));

        _logger.LogDebug("Query returned {Count} resources", results.Count);
        return results;
    }

    private static ResourceState? ParseResourceFromQuery(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idProp) ||
            !element.TryGetProperty("type", out var typeProp) ||
            !element.TryGetProperty("name", out var nameProp))
        {
            return null;
        }

        var id = idProp.GetString();
        var type = typeProp.GetString();
        var name = nameProp.GetString();

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(name))
        {
            return null;
        }

        string? location = null;
        if (element.TryGetProperty("location", out var locationProp))
        {
            location = locationProp.GetString();
        }

        string? resourceGroup = null;
        if (element.TryGetProperty("resourceGroup", out var rgProp))
        {
            resourceGroup = rgProp.GetString();
        }

        var tags = new Dictionary<string, string>();
        if (element.TryGetProperty("tags", out var tagsProp) &&
            tagsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var tag in tagsProp.EnumerateObject())
            {
                if (tag.Value.ValueKind == JsonValueKind.String)
                {
                    tags[tag.Name] = tag.Value.GetString() ?? "";
                }
            }
        }

        JsonElement properties = default;
        if (element.TryGetProperty("properties", out var propsProp))
        {
            properties = propsProp.Clone();
        }

        return new ResourceState
        {
            ResourceId = id,
            ResourceType = type,
            Name = name,
            Location = location,
            ResourceGroup = resourceGroup,
            Tags = tags,
            Properties = properties,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static string EscapeQueryString(string value)
    {
        return value.Replace("'", "''");
    }
}
