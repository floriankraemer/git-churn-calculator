namespace GitChurnCalculator.Console.Reporting;

public static class ChurnReportGeneratorFactory
{
    private static readonly IReadOnlyDictionary<string, IChurnReportGenerator> Generators =
        new Dictionary<string, IChurnReportGenerator>(StringComparer.OrdinalIgnoreCase)
        {
            ["table"] = new SpectreConsoleTableChurnReportGenerator(),
            ["csv"] = new CsvChurnReportGenerator(),
            ["json"] = new JsonChurnReportGenerator(),
            ["html"] = new HtmlTableChurnReportGenerator(),
            ["sarif"] = new SarifChurnReportGenerator(),
            ["github"] = new GithubActionsChurnReportGenerator(),
            ["gitlab"] = new GitlabCodeQualityChurnReportGenerator(),
        };

    public static bool TryGet(string format, out IChurnReportGenerator? generator)
        => Generators.TryGetValue(format.Trim(), out generator);

    public static string SupportedFormatsList => "table, csv, html, json, sarif, github, gitlab";
}
