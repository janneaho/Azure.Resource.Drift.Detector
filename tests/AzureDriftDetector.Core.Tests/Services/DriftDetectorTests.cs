using System.Text.Json;
using AzureDriftDetector.Core.Configuration;
using AzureDriftDetector.Core.Models;
using AzureDriftDetector.Core.Parsing;
using AzureDriftDetector.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AzureDriftDetector.Core.Tests.Services;

public class DriftDetectorTests
{
    private readonly DriftDetector _detector;
    private readonly IAzureResourceService _mockResourceService;
    private readonly TemplateParserFactory _parserFactory;
    private readonly DriftDetectorConfig _config;

    public DriftDetectorTests()
    {
        _mockResourceService = Substitute.For<IAzureResourceService>();
        _config = new DriftDetectorConfig();

        var armParser = new ArmTemplateParser(NullLogger<ArmTemplateParser>.Instance);
        var bicepParser = new BicepTemplateParser(
            NullLogger<BicepTemplateParser>.Instance,
            armParser);

        _parserFactory = new TemplateParserFactory([armParser, bicepParser]);

        _detector = new DriftDetector(
            _parserFactory,
            _mockResourceService,
            _config,
            NullLogger<DriftDetector>.Instance);
    }

    [Fact]
    public void Compare_WithIdenticalStates_ReturnsInSync()
    {
        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.InSync);
        result.Drifts.Should().BeEmpty();
    }

    [Fact]
    public void Compare_WithMissingActualResource_ReturnsMissing()
    {
        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");

        var result = _detector.Compare(expected, null);

        result.Status.Should().Be(DriftStatus.Missing);
    }

    [Fact]
    public void Compare_WithDifferentLocation_ReportsLocationDrift()
    {
        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts", location: "eastus");
        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts", location: "westus");

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d => d.PropertyPath == "location");
    }

    [Fact]
    public void Compare_WithDifferentTags_ReportsTagDrift()
    {
        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        expected = expected with { Tags = new Dictionary<string, string> { ["Env"] = "Prod" } };

        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        actual = actual with { Tags = new Dictionary<string, string> { ["Env"] = "Dev" } };

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d => d.PropertyPath == "tags.Env" && d.DriftType == DriftType.Modified);
    }

    [Fact]
    public void Compare_WithMissingTag_ReportsMissingTagDrift()
    {
        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        expected = expected with { Tags = new Dictionary<string, string> { ["Env"] = "Prod" } };

        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        actual = actual with { Tags = [] };

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d => d.PropertyPath == "tags.Env" && d.DriftType == DriftType.Missing);
    }

    [Fact]
    public void Compare_WithAddedTag_ReportsAddedTagDrift()
    {
        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        expected = expected with { Tags = [] };

        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts");
        actual = actual with { Tags = new Dictionary<string, string> { ["NewTag"] = "Value" } };

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d => d.PropertyPath == "tags.NewTag" && d.DriftType == DriftType.Added);
    }

    [Fact]
    public void Compare_WithDifferentProperties_ReportsPropertyDrift()
    {
        var expectedProps = JsonDocument.Parse("""{"tier": "Standard"}""").RootElement;
        var actualProps = JsonDocument.Parse("""{"tier": "Premium"}""").RootElement;

        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts") with
        {
            Properties = expectedProps
        };

        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts") with
        {
            Properties = actualProps
        };

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d =>
            d.PropertyPath == "properties.tier" &&
            d.DriftType == DriftType.Modified);
    }

    [Fact]
    public void Compare_WithNestedPropertyDifference_ReportsNestedDrift()
    {
        var expectedProps = JsonDocument.Parse("""{"sku": {"name": "Standard_LRS"}}""").RootElement;
        var actualProps = JsonDocument.Parse("""{"sku": {"name": "Premium_LRS"}}""").RootElement;

        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts") with
        {
            Properties = expectedProps
        };

        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts") with
        {
            Properties = actualProps
        };

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d =>
            d.PropertyPath == "properties.sku.name" &&
            d.DriftType == DriftType.Modified);
    }

    [Fact]
    public void Compare_WithArrayLengthDifference_ReportsLengthDrift()
    {
        var expectedProps = JsonDocument.Parse("""{"items": [1, 2, 3]}""").RootElement;
        var actualProps = JsonDocument.Parse("""{"items": [1, 2]}""").RootElement;

        var expected = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts") with
        {
            Properties = expectedProps
        };

        var actual = CreateResourceState("test-storage", "Microsoft.Storage/storageAccounts") with
        {
            Properties = actualProps
        };

        var result = _detector.Compare(expected, actual);

        result.Status.Should().Be(DriftStatus.Drifted);
        result.Drifts.Should().Contain(d =>
            d.PropertyPath == "properties.items.length");
    }

    private static ResourceState CreateResourceState(
        string name,
        string type,
        string? location = "eastus")
    {
        return new ResourceState
        {
            ResourceId = $"/subscriptions/sub/resourceGroups/rg/providers/{type}/{name}",
            ResourceType = type,
            Name = name,
            Location = location,
            Tags = []
        };
    }
}
