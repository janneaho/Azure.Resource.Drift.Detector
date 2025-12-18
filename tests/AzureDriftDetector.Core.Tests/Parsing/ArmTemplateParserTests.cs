using System.Text.Json;
using AzureDriftDetector.Core.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzureDriftDetector.Core.Tests.Parsing;

public class ArmTemplateParserTests : IDisposable
{
    private readonly ArmTemplateParser _parser;
    private readonly string _tempDir;

    public ArmTemplateParserTests()
    {
        _parser = new ArmTemplateParser(NullLogger<ArmTemplateParser>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void CanParse_WithJsonFile_ReturnsTrue()
    {
        _parser.CanParse("template.json").Should().BeTrue();
        _parser.CanParse("main.JSON").Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithNonJsonFile_ReturnsFalse()
    {
        _parser.CanParse("template.bicep").Should().BeFalse();
        _parser.CanParse("template.yaml").Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithValidTemplate_ReturnsResources()
    {
        var template = CreateValidArmTemplate();
        var filePath = Path.Combine(_tempDir, "template.json");
        await File.WriteAllTextAsync(filePath, template);

        var resources = await _parser.ParseAsync(filePath);

        resources.Should().HaveCount(1);
        resources[0].ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
        resources[0].Name.Should().Be("mystorageaccount");
        resources[0].Location.Should().Be("eastus");
    }

    [Fact]
    public async Task ParseAsync_WithParameters_ResolvesValues()
    {
        var template = CreateTemplateWithParameters();
        var filePath = Path.Combine(_tempDir, "template.json");
        await File.WriteAllTextAsync(filePath, template);

        var parameters = new Dictionary<string, string>
        {
            ["storageAccountName"] = "providedname"
        };

        var resources = await _parser.ParseAsync(filePath, parameters);

        resources.Should().HaveCount(1);
        resources[0].Name.Should().Be("providedname");
    }

    [Fact]
    public async Task ParseAsync_WithDefaultParameters_UsesDefaults()
    {
        var template = CreateTemplateWithParameters();
        var filePath = Path.Combine(_tempDir, "template.json");
        await File.WriteAllTextAsync(filePath, template);

        var resources = await _parser.ParseAsync(filePath);

        resources.Should().HaveCount(1);
        resources[0].Name.Should().Be("defaultstorage");
    }

    [Fact]
    public async Task ParseAsync_WithTags_ParsesTags()
    {
        var template = CreateTemplateWithTags();
        var filePath = Path.Combine(_tempDir, "template.json");
        await File.WriteAllTextAsync(filePath, template);

        var resources = await _parser.ParseAsync(filePath);

        resources[0].Tags.Should().ContainKey("Environment");
        resources[0].Tags["Environment"].Should().Be("Production");
    }

    [Fact]
    public async Task ParseAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        var act = () => _parser.ParseAsync("/nonexistent/path.json");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ParseAsync_WithInvalidArmTemplate_ThrowsInvalidOperationException()
    {
        var filePath = Path.Combine(_tempDir, "invalid.json");
        await File.WriteAllTextAsync(filePath, "{ \"notATemplate\": true }");

        var act = () => _parser.ParseAsync(filePath);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static string CreateValidArmTemplate()
    {
        return """
            {
                "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                "contentVersion": "1.0.0.0",
                "resources": [
                    {
                        "type": "Microsoft.Storage/storageAccounts",
                        "apiVersion": "2021-02-01",
                        "name": "mystorageaccount",
                        "location": "eastus",
                        "sku": {
                            "name": "Standard_LRS"
                        },
                        "kind": "StorageV2",
                        "properties": {
                            "supportsHttpsTrafficOnly": true
                        }
                    }
                ]
            }
            """;
    }

    private static string CreateTemplateWithParameters()
    {
        return """
            {
                "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                "contentVersion": "1.0.0.0",
                "parameters": {
                    "storageAccountName": {
                        "type": "string",
                        "defaultValue": "defaultstorage"
                    }
                },
                "resources": [
                    {
                        "type": "Microsoft.Storage/storageAccounts",
                        "apiVersion": "2021-02-01",
                        "name": "[parameters('storageAccountName')]",
                        "location": "eastus",
                        "sku": {
                            "name": "Standard_LRS"
                        },
                        "kind": "StorageV2"
                    }
                ]
            }
            """;
    }

    private static string CreateTemplateWithTags()
    {
        return """
            {
                "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                "contentVersion": "1.0.0.0",
                "resources": [
                    {
                        "type": "Microsoft.Storage/storageAccounts",
                        "apiVersion": "2021-02-01",
                        "name": "mystorageaccount",
                        "location": "eastus",
                        "tags": {
                            "Environment": "Production",
                            "CostCenter": "IT"
                        },
                        "sku": {
                            "name": "Standard_LRS"
                        },
                        "kind": "StorageV2"
                    }
                ]
            }
            """;
    }
}
