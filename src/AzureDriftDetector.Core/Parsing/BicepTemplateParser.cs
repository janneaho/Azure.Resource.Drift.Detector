using System.Diagnostics;
using System.Text.Json;
using AzureDriftDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureDriftDetector.Core.Parsing;

/// <summary>
/// Parses Bicep templates by compiling them to ARM and then parsing.
/// Requires the Bicep CLI to be installed.
/// </summary>
public sealed class BicepTemplateParser : ITemplateParser
{
    private readonly ILogger<BicepTemplateParser> _logger;
    private readonly ArmTemplateParser _armParser;
    private static readonly string[] SupportedExtensions = [".bicep"];

    public BicepTemplateParser(
        ILogger<BicepTemplateParser> logger,
        ArmTemplateParser armParser)
    {
        _logger = logger;
        _armParser = armParser;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<IReadOnlyList<ResourceState>> ParseAsync(
        string filePath,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Bicep file not found: {filePath}", filePath);
        }

        _logger.LogDebug("Compiling Bicep template: {FilePath}", filePath);

        var armJson = await CompileBicepToArmAsync(filePath, cancellationToken);

        // Write to temp file for ARM parser
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, armJson, cancellationToken);
            return await _armParser.ParseAsync(tempFile, parameters, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<string> CompileBicepToArmAsync(
        string bicepFilePath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "bicep",
            Arguments = $"build \"{bicepFilePath}\" --stdout",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start Bicep CLI. Ensure 'bicep' is installed and available in PATH.",
                ex);
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Bicep compilation failed with exit code {process.ExitCode}: {error}");
        }

        // Validate it's valid JSON
        try
        {
            using var doc = JsonDocument.Parse(output);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Bicep compilation produced invalid JSON", ex);
        }

        _logger.LogDebug("Successfully compiled Bicep to ARM template");
        return output;
    }
}
