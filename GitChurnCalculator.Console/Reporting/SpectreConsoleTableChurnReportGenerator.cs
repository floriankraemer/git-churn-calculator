using System.Globalization;
using GitChurnCalculator.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitChurnCalculator.Console.Reporting;

public sealed class SpectreConsoleTableChurnReportGenerator : IChurnReportGenerator
{
    private const int MaxFilePathLength = 60;

    public string Generate(IReadOnlyList<FileChurnResult> results, string subtitle)
    {
        _ = subtitle;

        var showCoverage = results.Any(r => r.CoveragePercent.HasValue);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("File").LeftAligned());
        table.AddColumn(new TableColumn("Commits").RightAligned());
        table.AddColumn(new TableColumn("+Lines").RightAligned());
        table.AddColumn(new TableColumn("-Lines").RightAligned());
        table.AddColumn(new TableColumn("Last Commit"));
        table.AddColumn(new TableColumn("Authors").RightAligned());
        if (showCoverage)
            table.AddColumn(new TableColumn("Coverage %").RightAligned());
        table.AddColumn(new TableColumn("Churn Risk").RightAligned());

        foreach (var r in results)
        {
            var row = new List<IRenderable>
            {
                new Markup(Markup.Escape(TruncatePath(r.FilePath))),
                new Text(r.TotalCommits.ToString(CultureInfo.InvariantCulture)),
                new Text(r.LinesAdded.ToString(CultureInfo.InvariantCulture)),
                new Text(r.LinesRemoved.ToString(CultureInfo.InvariantCulture)),
                new Text(r.LastCommitDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-"),
                new Text(r.TotalUniqueAuthors.ToString(CultureInfo.InvariantCulture)),
            };

            if (showCoverage)
            {
                row.Add(new Text(r.CoveragePercent?.ToString("F2", CultureInfo.InvariantCulture) ?? "-"));
            }

            row.Add(FormatChurnRisk(r.ChurnRiskScore));
            table.AddRow(row.ToArray());
        }

        AnsiConsole.Write(table);
        return string.Empty;
    }

    private static Markup FormatChurnRisk(double score)
    {
        var text = score.ToString("F4", CultureInfo.InvariantCulture);
        var color = score >= 0.7 ? "red" : score >= 0.3 ? "yellow" : "green";
        return new Markup($"[{color}]{text}[/]");
    }

    private static string TruncatePath(string path)
    {
        if (path.Length <= MaxFilePathLength)
            return path;

        return "..." + path[(path.Length - (MaxFilePathLength - 3))..];
    }
}
