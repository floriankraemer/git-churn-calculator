using System.Globalization;
using System.Xml;

namespace GitChurnCalculator.Services;

/// <summary>
/// Parses Cobertura XML using a streaming <see cref="XmlReader"/> so large reports
/// with per-line elements are not loaded entirely into memory.
/// </summary>
public sealed class CoberturaXmlParser : ICoverageParser
{
    public Dictionary<string, double> MapToTrackedFiles(
        Dictionary<string, double> coverageByPath,
        IReadOnlyList<string> trackedGitRelativePaths) =>
        CoveragePathMatcher.MapToGitFiles(coverageByPath, trackedGitRelativePaths);

    public Dictionary<string, double> Parse(string coverageFilePath)
    {
        var sourcePrefixes = new List<string>();
        var coverage = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        using var reader = XmlReader.Create(
            coverageFilePath,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.Name)
            {
                case "source" when !reader.IsEmptyElement:
                    var source = CoveragePathMatcher.NormalizePath(reader.ReadElementContentAsString());
                    if (source.Length > 0)
                        sourcePrefixes.Add(source);
                    break;

                case "class":
                    TryAddClassCoverage(reader, sourcePrefixes, coverage);
                    break;
            }
        }

        return coverage;
    }

    private static void TryAddClassCoverage(
        XmlReader reader,
        List<string> sourcePrefixes,
        Dictionary<string, double> coverage)
    {
        var filename = reader.GetAttribute("filename");
        var lineRateStr = reader.GetAttribute("line-rate");

        if (reader.IsEmptyElement)
            reader.Read();
        else
            reader.Skip();

        if (string.IsNullOrWhiteSpace(filename))
            return;

        if (lineRateStr is null ||
            !double.TryParse(lineRateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lineRate))
            return;

        var coveragePercent = lineRate * 100.0;
        var normalized = CoveragePathMatcher.NormalizePath(filename);
        var relativePath = MakeRelative(normalized, sourcePrefixes);

        if (!coverage.TryGetValue(relativePath, out var existing) || coveragePercent > existing)
            coverage[relativePath] = coveragePercent;
    }

    private static string MakeRelative(string normalizedPath, List<string> sourcePrefixes)
    {
        foreach (var prefix in sourcePrefixes)
        {
            var prefixWithSlash = prefix.EndsWith('/') ? prefix : prefix + "/";
            if (!normalizedPath.StartsWith(prefixWithSlash, StringComparison.OrdinalIgnoreCase))
                continue;
            return normalizedPath[prefixWithSlash.Length..];
        }

        return normalizedPath;
    }
}
