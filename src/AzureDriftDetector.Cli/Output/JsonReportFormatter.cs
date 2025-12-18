using System.Text.Json;
using System.Text.Json.Serialization;
using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Cli.Output;

public sealed class JsonReportFormatter : IReportFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Format(DriftReport report)
    {
        return JsonSerializer.Serialize(report, Options);
    }
}
