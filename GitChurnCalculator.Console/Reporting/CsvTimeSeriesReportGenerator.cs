using System.Globalization;
using System.Text;
using GitChurnCalculator.Models;

namespace GitChurnCalculator.Console.Reporting;

public sealed class CsvTimeSeriesReportGenerator : ITimeSeriesReportGenerator
{
    public string Generate(IReadOnlyList<TimeSeriesPoint> points, string subtitle)
    {
        _ = subtitle;
        var sb = new StringBuilder();
        sb.AppendLine("AsOf,File,TotalCommits,LinesAdded,LinesRemoved,FirstCommitDate,LastCommitDate,AgeDays,ChangesPerWeek,ChangesPerMonth,ChangesPerYear,CommitsLast7Days,CommitsLast30Days,CommitsLast365Days,TotalUniqueAuthors,UniqueAuthorsLast7Days,UniqueAuthorsLast30Days,UniqueAuthorsLast365Days,CoveragePercent,ChurnRiskScore");

        foreach (var point in points)
        {
            var asOf = point.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var r in point.Files)
            {
                sb.Append(asOf);
                sb.Append(',').Append('"').Append(r.FilePath.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                sb.Append(',').Append(r.TotalCommits);
                sb.Append(',').Append(r.LinesAdded);
                sb.Append(',').Append(r.LinesRemoved);
                sb.Append(',').Append(r.FirstCommitDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
                sb.Append(',').Append(r.LastCommitDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
                sb.Append(',').Append(r.AgeDays);
                sb.Append(',').Append(r.ChangesPerWeek.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',').Append(r.ChangesPerMonth.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',').Append(r.ChangesPerYear.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',').Append(r.CommitsLast7Days);
                sb.Append(',').Append(r.CommitsLast30Days);
                sb.Append(',').Append(r.CommitsLast365Days);
                sb.Append(',').Append(r.TotalUniqueAuthors);
                sb.Append(',').Append(r.UniqueAuthorsLast7Days);
                sb.Append(',').Append(r.UniqueAuthorsLast30Days);
                sb.Append(',').Append(r.UniqueAuthorsLast365Days);
                sb.Append(',').Append(r.CoveragePercent?.ToString("F2", CultureInfo.InvariantCulture) ?? "");
                sb.Append(',').Append(r.ChurnRiskScore.ToString("F4", CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
