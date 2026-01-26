using System.CommandLine;
using AzureDriftDetector.Core.Integrations;
using AzureDriftDetector.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Cli.Commands;

public static class DevOpsCommand
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

        var orgUrlOption = new Option<string>("--org-url")
        {
            Description = "Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)",
            Required = true
        };

        var projectOption = new Option<string>("--project")
        {
            Description = "Azure DevOps project name",
            Required = true
        };

        var prIdOption = new Option<int>("--pr-id")
        {
            Description = "Pull request ID to comment on",
            Required = true
        };

        var tokenOption = new Option<string>("--token")
        {
            Description = "Azure DevOps personal access token (or set AZURE_DEVOPS_PAT env var)",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT") ?? ""
        };

        var commentIdOption = new Option<string?>("--comment-id")
        {
            Description = "Identifier for upsert behavior (updates existing comment if found)"
        };

        var command = new Command("devops", "Post drift report as Azure DevOps PR comment");
        command.Options.Add(templateOption);
        command.Options.Add(subscriptionOption);
        command.Options.Add(resourceGroupOption);
        command.Options.Add(orgUrlOption);
        command.Options.Add(projectOption);
        command.Options.Add(prIdOption);
        command.Options.Add(tokenOption);
        command.Options.Add(commentIdOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var template = parseResult.GetValue(templateOption)!;
            var subscription = parseResult.GetValue(subscriptionOption)!;
            var resourceGroup = parseResult.GetValue(resourceGroupOption)!;
            var orgUrl = parseResult.GetValue(orgUrlOption)!;
            var project = parseResult.GetValue(projectOption)!;
            var prId = parseResult.GetValue(prIdOption);
            var token = parseResult.GetValue(tokenOption);
            var commentId = parseResult.GetValue(commentIdOption);

            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("Error: Azure DevOps PAT is required. Use --token or set AZURE_DEVOPS_PAT environment variable.");
                return 1;
            }

            var detector = services.GetRequiredService<IDriftDetector>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();

            var report = await detector.GenerateReportAsync(
                template.FullName,
                subscription,
                resourceGroup,
                cancellationToken: cancellationToken);

            using var devOpsClient = new AzureDevOpsClient(
                token,
                loggerFactory.CreateLogger<AzureDevOpsClient>());

            if (!string.IsNullOrEmpty(commentId))
            {
                await devOpsClient.UpsertPullRequestCommentAsync(
                    orgUrl,
                    project,
                    prId,
                    report,
                    commentId,
                    cancellationToken);
            }
            else
            {
                await devOpsClient.PostPullRequestCommentAsync(
                    orgUrl,
                    project,
                    prId,
                    report,
                    cancellationToken);
            }

            Console.WriteLine($"Successfully posted drift report to PR #{prId}");

            if (report.HasDrift)
            {
                return 1;
            }

            return 0;
        });

        return command;
    }
}
