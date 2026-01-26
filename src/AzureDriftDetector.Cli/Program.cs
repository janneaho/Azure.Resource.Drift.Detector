using System.CommandLine;
using Azure.Identity;
using Azure.ResourceManager;
using AzureDriftDetector.Cli.Commands;
using AzureDriftDetector.Core.Configuration;
using AzureDriftDetector.Core.Parsing;
using AzureDriftDetector.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Build service provider
var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(
        args.Contains("-v") || args.Contains("--verbose")
            ? LogLevel.Debug
            : LogLevel.Warning);
});

services.AddSingleton(_ => new ArmClient(new DefaultAzureCredential()));
services.AddSingleton<DriftDetectorConfig>();
services.AddSingleton<ArmTemplateParser>();
services.AddSingleton<BicepTemplateParser>();
services.AddSingleton<ITemplateParser>(sp => sp.GetRequiredService<ArmTemplateParser>());
services.AddSingleton<ITemplateParser>(sp => sp.GetRequiredService<BicepTemplateParser>());
services.AddSingleton<TemplateParserFactory>();
services.AddSingleton<IAzureResourceService, AzureResourceGraphService>();
services.AddSingleton<IDriftDetector, DriftDetector>();

var serviceProvider = services.BuildServiceProvider();

var verboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "Enable verbose output"
};

var rootCommand = new RootCommand("Azure Resource Drift Detector - Know when reality diverges from your IaC");
rootCommand.Subcommands.Add(DetectCommand.Create(serviceProvider));
rootCommand.Subcommands.Add(DevOpsCommand.Create(serviceProvider));
rootCommand.Subcommands.Add(NotifyCommand.Create(serviceProvider));
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Options.Add(verboseOption);

var result = rootCommand.Parse(args);
return await result.InvokeAsync();
