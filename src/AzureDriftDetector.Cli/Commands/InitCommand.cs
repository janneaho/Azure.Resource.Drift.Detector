using System.CommandLine;
using AzureDriftDetector.Core.Configuration;

namespace AzureDriftDetector.Cli.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var directoryOption = new Option<DirectoryInfo?>("--directory", "-d")
        {
            Description = "Directory to create config file in (defaults to current directory)",
            DefaultValueFactory = _ => null
        };

        var command = new Command("init", "Create a sample .driftdetector.json configuration file");
        command.Options.Add(directoryOption);

        command.SetAction((parseResult) =>
        {
            var directory = parseResult.GetValue(directoryOption);
            var targetDir = directory?.FullName ?? Directory.GetCurrentDirectory();

            var configPath = Path.Combine(targetDir, ".driftdetector.json");

            if (File.Exists(configPath))
            {
                Console.Error.WriteLine($"Configuration file already exists: {configPath}");
                return 1;
            }

            DriftDetectorConfigFile.SaveSampleConfig(targetDir);
            Console.WriteLine($"Created sample configuration file: {configPath}");
            return 0;
        });

        return command;
    }
}
