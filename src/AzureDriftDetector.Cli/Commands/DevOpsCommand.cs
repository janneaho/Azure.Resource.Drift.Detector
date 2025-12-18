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

        var orgUrlOption = new Option<string>(
            "--org-url",
            "Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)")
        {
            IsRequired = true
        };

        var projectOption = new Option<string>(
            "--project",
            "Azure DevOps project name")
        {
            IsRequired = true
        };

        var prIdOption = new Option<int>(
            "--pr-id",
            "Pull request ID to comment on")
        {
            IsRequired = true
        };

        var tokenOption = new Option<string>(
            "--token",
            () => Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT") ?? "",
            "Azure DevOps personal access token (or set AZURE_DEVOPS_PAT env var)");

        var commentIdOption = new Option<string?>(
            "--comment-id",
            "Identifier for upsert behavior (updates existing comment if found)");

        var command = new Command("devops", "Post drift report as Azure DevOps PR comment")
        {
            templateOption,
            subscriptionOption,
            resourceGroupOption,
            orgUrlOption,
            projectOption,
            prIdOption,
            tokenOption,
            commentIdOption
        };

        command.SetHandler(async (context) =>
        {
            var template = context.ParseResult.GetValueForOption(templateOption)!;
            var subscription = context.ParseResult.GetValueForOption(subscriptionOption)!;
            var resourceGroup = context.ParseResult.GetValueForOption(resourceGroupOption)!;
            var orgUrl = context.ParseResult.GetValueForOption(orgUrlOption)!;
            var project = context.ParseResult.GetValueForOption(projectOption)!;
            var prId = context.ParseResult.GetValueForOption(prIdOption);
            var token = context.ParseResult.GetValueForOption(tokenOption);
            var commentId = context.ParseResult.GetValueForOption(commentIdOption);

            if (string.IsNullOrEmpty(token))
            {
                Console.Error.WriteLine("Error: Azure DevOps PAT is required. Use --token or set AZURE_DEVOPS_PAT environment variable.");
                context.ExitCode = 1;
                return;
            }

            var detector = services.GetRequiredService<IDriftDetector>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();

            var report = await detector.GenerateReportAsync(
                template.FullName,
                subscription,
                resourceGroup,
                cancellationToken: context.GetCancellationToken());

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
                    context.GetCancellationToken());
            }
            else
            {
                await devOpsClient.PostPullRequestCommentAsync(
                    orgUrl,
                    project,
                    prId,
                    report,
                    context.GetCancellationToken());
            }

            Console.WriteLine($"Successfully posted drift report to PR #{prId}");

            if (report.HasDrift)
            {
                context.ExitCode = 1;
            }
        });

        return command;
    }
}
