namespace AzureDriftDetector.Core.Parsing;

/// <summary>
/// Factory for selecting the appropriate template parser based on file type.
/// </summary>
public sealed class TemplateParserFactory
{
    private readonly IEnumerable<ITemplateParser> _parsers;

    public TemplateParserFactory(IEnumerable<ITemplateParser> parsers)
    {
        _parsers = parsers;
    }

    public ITemplateParser GetParser(string filePath)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(filePath))
            {
                return parser;
            }
        }

        var extension = Path.GetExtension(filePath);
        throw new NotSupportedException(
            $"No parser available for file type: {extension}. Supported types: .bicep, .json");
    }
}
