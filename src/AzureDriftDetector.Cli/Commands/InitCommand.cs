using System.CommandLine;
using AzureDriftDetector.Core.Configuration;

namespace AzureDriftDetector.Cli.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var directoryOption = new Option<DirectoryInfo?>(
            ["--directory", "-d"],
            () => null,
            "Directory to create config file in (defaults to current directory)");

        var command = new Command("init", "Create a sample .driftdetector.json configuration file")
        {
            directoryOption
        };

        command.SetHandler((context) =>
        {
            var directory = context.ParseResult.GetValueForOption(directoryOption);
            var targetDir = directory?.FullName ?? Directory.GetCurrentDirectory();

            var configPath = Path.Combine(targetDir, ".driftdetector.json");

            if (File.Exists(configPath))
            {
                Console.Error.WriteLine($"Configuration file already exists: {configPath}");
                context.ExitCode = 1;
                return;
            }

            DriftDetectorConfigFile.SaveSampleConfig(targetDir);
            Console.WriteLine($"Created sample configuration file: {configPath}");
        });

        return command;
    }
}
