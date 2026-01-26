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

        var slackWebhookOption = new Option<string?>("--slack-webhook")
        {
            Description = "Slack webhook URL (or set SLACK_WEBHOOK_URL env var)",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL")
        };

        var teamsWebhookOption = new Option<string?>("--teams-webhook")
        {
            Description = "Microsoft Teams webhook URL (or set TEAMS_WEBHOOK_URL env var)",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("TEAMS_WEBHOOK_URL")
        };

        var onlyOnDriftOption = new Option<bool>("--only-on-drift")
        {
            Description = "Only send notification if drift is detected",
            DefaultValueFactory = _ => false
        };

        var command = new Command("notify", "Send drift report notifications to Slack/Teams");
        command.Options.Add(templateOption);
        command.Options.Add(subscriptionOption);
        command.Options.Add(resourceGroupOption);
        command.Options.Add(slackWebhookOption);
        command.Options.Add(teamsWebhookOption);
        command.Options.Add(onlyOnDriftOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var template = parseResult.GetValue(templateOption)!;
            var subscription = parseResult.GetValue(subscriptionOption)!;
            var resourceGroup = parseResult.GetValue(resourceGroupOption)!;
            var slackWebhook = parseResult.GetValue(slackWebhookOption);
            var teamsWebhook = parseResult.GetValue(teamsWebhookOption);
            var onlyOnDrift = parseResult.GetValue(onlyOnDriftOption);

            if (string.IsNullOrEmpty(slackWebhook) && string.IsNullOrEmpty(teamsWebhook))
            {
                Console.Error.WriteLine("Error: At least one webhook URL is required (--slack-webhook or --teams-webhook)");
                return 1;
            }

            var detector = services.GetRequiredService<IDriftDetector>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();

            var report = await detector.GenerateReportAsync(
                template.FullName,
                subscription,
                resourceGroup,
                cancellationToken: cancellationToken);

            if (onlyOnDrift && !report.HasDrift)
            {
                Console.WriteLine("No drift detected. Skipping notification.");
                return 0;
            }

            if (!string.IsNullOrEmpty(slackWebhook))
            {
                using var slackClient = new SlackNotificationClient(
                    slackWebhook,
                    loggerFactory.CreateLogger<SlackNotificationClient>());

                await slackClient.SendNotificationAsync(report, cancellationToken);
                Console.WriteLine("Sent notification to Slack");
            }

            if (!string.IsNullOrEmpty(teamsWebhook))
            {
                using var teamsClient = new TeamsNotificationClient(
                    teamsWebhook,
                    loggerFactory.CreateLogger<TeamsNotificationClient>());

                await teamsClient.SendNotificationAsync(report, cancellationToken);
                Console.WriteLine("Sent notification to Microsoft Teams");
            }

            if (report.HasDrift)
            {
                return 1;
            }

            return 0;
        });

        return command;
    }
}
