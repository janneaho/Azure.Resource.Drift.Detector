using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Cli.Output;

public interface IReportFormatter
{
    string Format(DriftReport report);
}
