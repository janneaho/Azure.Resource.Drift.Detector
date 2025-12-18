using System.CommandLine;
using AzureDriftDetector.Core.Integrations;
using AzureDriftDetector.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Cli.Commands;

public static class NotifyCommand
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

        var slackWebhookOption = new Option<string?>(
            "--slack-webhook",
            () => Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL"),
            "Slack webhook URL (or set SLACK_WEBHOOK_URL env var)");

        var teamsWebhookOption = new Option<string?>(
            "--teams-webhook",
            () => Environment.GetEnvironmentVariable("TEAMS_WEBHOOK_URL"),
            "Microsoft Teams webhook URL (or set TEAMS_WEBHOOK_URL env var)");

        var onlyOnDriftOption = new Option<bool>(
            "--only-on-drift",
            () => false,
            "Only send notification if drift is detected");

        var command = new Command("notify", "Send drift report notifications to Slack/Teams")
        {
            templateOption,
            subscriptionOption,
            resourceGroupOption,
            slackWebhookOption,
            teamsWebhookOption,
            onlyOnDriftOption
        };

        command.SetHandler(async (context) =>
        {
            var template = context.ParseResult.GetValueForOption(templateOption)!;
            var subscription = context.ParseResult.GetValueForOption(subscriptionOption)!;
            var resourceGroup = context.ParseResult.GetValueForOption(resourceGroupOption)!;
            var slackWebhook = context.ParseResult.GetValueForOption(slackWebhookOption);
            var teamsWebhook = context.ParseResult.GetValueForOption(teamsWebhookOption);
            var onlyOnDrift = context.ParseResult.GetValueForOption(onlyOnDriftOption);

            if (string.IsNullOrEmpty(slackWebhook) && string.IsNullOrEmpty(teamsWebhook))
            {
                Console.Error.WriteLine("Error: At least one webhook URL is required (--slack-webhook or --teams-webhook)");
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

            if (onlyOnDrift && !report.HasDrift)
            {
                Console.WriteLine("No drift detected. Skipping notification.");
                return;
            }

            if (!string.IsNullOrEmpty(slackWebhook))
            {
                using var slackClient = new SlackNotificationClient(
                    slackWebhook,
                    loggerFactory.CreateLogger<SlackNotificationClient>());

                await slackClient.SendNotificationAsync(report, context.GetCancellationToken());
                Console.WriteLine("Sent notification to Slack");
            }

            if (!string.IsNullOrEmpty(teamsWebhook))
            {
                using var teamsClient = new TeamsNotificationClient(
                    teamsWebhook,
                    loggerFactory.CreateLogger<TeamsNotificationClient>());

                await teamsClient.SendNotificationAsync(report, context.GetCancellationToken());
                Console.WriteLine("Sent notification to Microsoft Teams");
            }

            if (report.HasDrift)
            {
                context.ExitCode = 1;
            }
        });

        return command;
    }
}
