using GitChurnCalculator.Models;

namespace GitChurnCalculator.Console.Reporting;

/// <summary>
/// Maps ChurnRiskScore to CI-friendly severities. Bands: [0,1) note, [1,10) warning, [10,∞) error.
/// </summary>
internal static class ChurnCiSeverity
{
    internal static string SarifLevel(double score) => score switch
    {
        < 1.0 => "note",
        < 10.0 => "warning",
        _ => "error",
    };

    internal static string GitlabSeverity(double score) => score switch
    {
        < 1.0 => "info",
        < 10.0 => "minor",
        _ => "major",
    };

    /// <summary>GitHub workflow command: notice, warning, or error.</summary>
    internal static string GithubCommandKind(double score) => score switch
    {
        < 1.0 => "notice",
        < 10.0 => "warning",
        _ => "error",
    };

    internal static string BuildMessage(FileChurnResult r)
    {
        return $"Churn risk score {r.ChurnRiskScore:F4} (commits={r.TotalCommits}, +{r.LinesAdded}/-{r.LinesRemoved}, authors={r.TotalUniqueAuthors}, coverage={FormatCoverage(r.CoveragePercent)})";
    }

    private static string FormatCoverage(double? p) => p.HasValue ? $"{p.Value:F1}%" : "n/a";
}
