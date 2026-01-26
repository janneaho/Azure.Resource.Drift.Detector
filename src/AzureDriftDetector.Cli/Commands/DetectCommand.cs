using System.CommandLine;
using AzureDriftDetector.Cli.Output;
using AzureDriftDetector.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AzureDriftDetector.Cli.Commands;

public static class DetectCommand
{
    public static Command Create(IServiceProvider services)
    {
        var templateOption = new Option<FileInfo>("--template", "-t")
        {
            Description = "Path to Bicep or ARM template file",
            Required = true
        };

        var subscriptionOption = new Option<string>("--subscription", "-s")
        {
            Description = "Azure subscription ID",
            Required = true
        };

        var resourceGroupOption = new Option<string>("--resource-group", "-g")
        {
            Description = "Azure resource group name",
            Required = true
        };

        var outputFormatOption = new Option<OutputFormat>("--output", "-o")
        {
            Description = "Output format (console, json, markdown)",
            DefaultValueFactory = _ => OutputFormat.Console
        };

        var outputFileOption = new Option<FileInfo?>("--output-file")
        {
            Description = "Write output to file instead of stdout"
        };

        var failOnDriftOption = new Option<bool>("--fail-on-drift")
        {
            Description = "Exit with non-zero code if drift is detected",
            DefaultValueFactory = _ => false
        };

        var parameterOption = new Option<string[]>("--parameter", "-p")
        {
            Description = "Template parameters (key=value format)",
            AllowMultipleArgumentsPerToken = true
        };

        var command = new Command("detect", "Detect drift between template and Azure resources");
        command.Options.Add(templateOption);
        command.Options.Add(subscriptionOption);
        command.Options.Add(resourceGroupOption);
        command.Options.Add(outputFormatOption);
        command.Options.Add(outputFileOption);
        command.Options.Add(failOnDriftOption);
        command.Options.Add(parameterOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var template = parseResult.GetValue(templateOption)!;
            var subscription = parseResult.GetValue(subscriptionOption)!;
            var resourceGroup = parseResult.GetValue(resourceGroupOption)!;
            var outputFormat = parseResult.GetValue(outputFormatOption);
            var outputFile = parseResult.GetValue(outputFileOption);
            var failOnDrift = parseResult.GetValue(failOnDriftOption);
            var parameters = parseResult.GetValue(parameterOption);

            var detector = services.GetRequiredService<IDriftDetector>();

            var paramDict = ParseParameters(parameters);

            var report = await detector.GenerateReportAsync(
                template.FullName,
                subscription,
                resourceGroup,
                paramDict,
                cancellationToken);

            var formatter = ReportFormatterFactory.Create(outputFormat);
            var output = formatter.Format(report);

            if (outputFile != null)
            {
                await File.WriteAllTextAsync(outputFile.FullName, output, cancellationToken);
                Console.WriteLine($"Report written to: {outputFile.FullName}");
            }
            else
            {
                Console.WriteLine(output);
            }

            if (failOnDrift && report.HasDrift)
            {
                return 1;
            }

            return 0;
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
