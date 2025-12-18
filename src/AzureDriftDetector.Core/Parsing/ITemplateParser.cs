using AzureDriftDetector.Core.Models;

namespace AzureDriftDetector.Core.Parsing;

/// <summary>
/// Parses infrastructure templates to extract expected resource state.
/// </summary>
public interface ITemplateParser
{
    /// <summary>
    /// Determines if this parser can handle the given file.
    /// </summary>
    bool CanParse(string filePath);

    /// <summary>
    /// Parses a template file and returns expected resource states.
    /// </summary>
    Task<IReadOnlyList<ResourceState>> ParseAsync(
        string filePath,
        IDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);
}
