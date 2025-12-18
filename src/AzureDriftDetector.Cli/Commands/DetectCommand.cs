using System.CommandLine;
using AzureDriftDetector.Cli.Output;
using AzureDriftDetector.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AzureDriftDetector.Cli.Commands;

public static class DetectCommand
{
    public static Command Create(IServiceProvider services)
    {
        var templateOption = new Option<FileInfo>(
            ["--template", "-t"],
            "Path to Bicep or ARM template file")
        {
            IsRequired = true
        };

        var subscriptionOption = new Option<string>(
            ["--subscription", "-s"],
            "Azure subscription ID")
        {
            IsRequired = true
        };

        var resourceGroupOption = new Option<string>(
            ["--resource-group", "-g"],
            "Azure resource group name")
        {
            IsRequired = true
        };

        var outputFormatOption = new Option<OutputFormat>(
            ["--output", "-o"],
            () => OutputFormat.Console,
            "Output format (console, json, markdown)");

        var outputFileOption = new Option<FileInfo?>(
            "--output-file",
            "Write output to file instead of stdout");

        var failOnDriftOption = new Option<bool>(
            "--fail-on-drift",
            () => false,
            "Exit with non-zero code if drift is detected");

        var parameterOption = new Option<string[]>(
            ["--parameter", "-p"],
            "Template parameters (key=value format)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var command = new Command("detect", "Detect drift between template and Azure resources")
        {
            templateOption,
            subscriptionOption,
            resourceGroupOption,
            outputFormatOption,
            outputFileOption,
            failOnDriftOption,
            parameterOption
        };

        command.SetHandler(async (context) =>
        {
            var template = context.ParseResult.GetValueForOption(templateOption)!;
            var subscription = context.ParseResult.GetValueForOption(subscriptionOption)!;
            var resourceGroup = context.ParseResult.GetValueForOption(resourceGroupOption)!;
            var outputFormat = context.ParseResult.GetValueForOption(outputFormatOption);
            var outputFile = context.ParseResult.GetValueForOption(outputFileOption);
            var failOnDrift = context.ParseResult.GetValueForOption(failOnDriftOption);
            var parameters = context.ParseResult.GetValueForOption(parameterOption);

            var detector = services.GetRequiredService<IDriftDetector>();

            var paramDict = ParseParameters(parameters);

            var report = await detector.GenerateReportAsync(
                template.FullName,
                subscription,
                resourceGroup,
                paramDict,
                context.GetCancellationToken());

            var formatter = ReportFormatterFactory.Create(outputFormat);
            var output = formatter.Format(report);

            if (outputFile != null)
            {
                await File.WriteAllTextAsync(outputFile.FullName, output);
                Console.WriteLine($"Report written to: {outputFile.FullName}");
            }
            else
            {
                Console.WriteLine(output);
            }

            if (failOnDrift && report.HasDrift)
            {
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Dictionary<string, string>? ParseParameters(string[]? parameters)
    {
        if (parameters == null || parameters.Length == 0)
            return null;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in parameters)
        {
            var parts = param.Split('=', 2);
            if (parts.Length == 2)
            {
                dict[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return dict.Count > 0 ? dict : null;
    }
}

public enum OutputFormat
{
    Console,
    Json,
    Markdown
}
