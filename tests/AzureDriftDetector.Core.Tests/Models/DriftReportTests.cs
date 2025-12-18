using AzureDriftDetector.Core.Models;
using FluentAssertions;

namespace AzureDriftDetector.Core.Tests.Models;

public class DriftReportTests
{
    [Fact]
    public void TotalResources_ReturnsCorrectCount()
    {
        var report = CreateReport(
            DriftStatus.InSync,
            DriftStatus.Drifted,
            DriftStatus.Missing);

        report.TotalResources.Should().Be(3);
    }

    [Fact]
    public void InSyncCount_ReturnsCorrectCount()
    {
        var report = CreateReport(
            DriftStatus.InSync,
            DriftStatus.InSync,
            DriftStatus.Drifted);

        report.InSyncCount.Should().Be(2);
    }

    [Fact]
    public void DriftedCount_ReturnsCorrectCount()
    {
        var report = CreateReport(
            DriftStatus.Drifted,
            DriftStatus.Drifted,
            DriftStatus.InSync);

        report.DriftedCount.Should().Be(2);
    }

    [Fact]
    public void MissingCount_ReturnsCorrectCount()
    {
        var report = CreateReport(
            DriftStatus.Missing,
            DriftStatus.InSync);

        report.MissingCount.Should().Be(1);
    }

    [Fact]
    public void UnmanagedCount_ReturnsCorrectCount()
    {
        var report = CreateReport(
            DriftStatus.Unmanaged,
            DriftStatus.Unmanaged,
            DriftStatus.InSync);

        report.UnmanagedCount.Should().Be(2);
    }

    [Fact]
    public void HasDrift_WithDriftedResources_ReturnsTrue()
    {
        var report = CreateReport(DriftStatus.InSync, DriftStatus.Drifted);

        report.HasDrift.Should().BeTrue();
    }

    [Fact]
    public void HasDrift_WithMissingResources_ReturnsTrue()
    {
        var report = CreateReport(DriftStatus.InSync, DriftStatus.Missing);

        report.HasDrift.Should().BeTrue();
    }

    [Fact]
    public void HasDrift_WithOnlyInSyncResources_ReturnsFalse()
    {
        var report = CreateReport(DriftStatus.InSync, DriftStatus.InSync);

        report.HasDrift.Should().BeFalse();
    }

    [Fact]
    public void HasDrift_WithOnlyUnmanagedResources_ReturnsFalse()
    {
        var report = CreateReport(DriftStatus.InSync, DriftStatus.Unmanaged);

        report.HasDrift.Should().BeFalse();
    }

    private static DriftReport CreateReport(params DriftStatus[] statuses)
    {
        var results = statuses.Select((status, i) => new DriftResult
        {
            ResourceId = $"/subscriptions/sub/resourceGroups/rg/providers/Type/resource{i}",
            ResourceType = "Microsoft.Test/resources",
            ResourceName = $"resource{i}",
            Status = status
        }).ToList();

        return new DriftReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            TemplatePath = "/path/to/template.json",
            SubscriptionId = "sub-123",
            ResourceGroup = "test-rg",
            Results = results
        };
    }
}

public class DriftResultTests
{
    [Fact]
    public void HasDrift_WhenStatusIsDrifted_ReturnsTrue()
    {
        var result = new DriftResult
        {
            ResourceId = "/test",
            ResourceType = "Microsoft.Test/test",
            ResourceName = "test",
            Status = DriftStatus.Drifted
        };

        result.HasDrift.Should().BeTrue();
    }

    [Fact]
    public void HasDrift_WhenStatusIsInSync_ReturnsFalse()
    {
        var result = new DriftResult
        {
            ResourceId = "/test",
            ResourceType = "Microsoft.Test/test",
            ResourceName = "test",
            Status = DriftStatus.InSync
        };

        result.HasDrift.Should().BeFalse();
    }

    [Fact]
    public void HasDrift_WhenStatusIsMissing_ReturnsFalse()
    {
        var result = new DriftResult
        {
            ResourceId = "/test",
            ResourceType = "Microsoft.Test/test",
            ResourceName = "test",
            Status = DriftStatus.Missing
        };

        result.HasDrift.Should().BeFalse();
    }
}
