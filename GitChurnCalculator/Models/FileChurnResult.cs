namespace GitChurnCalculator.Models;

public sealed class FileChurnResult
{
    public required string FilePath { get; init; }
    public int TotalCommits { get; init; }
    public int LinesAdded { get; init; }
    public int LinesRemoved { get; init; }
    public DateTime? FirstCommitDate { get; init; }
    public DateTime? LastCommitDate { get; init; }
    public int AgeDays { get; init; }
    public double ChangesPerWeek { get; init; }
    public double ChangesPerMonth { get; init; }
    public double ChangesPerYear { get; init; }
    public int CommitsLast7Days { get; init; }
    public int CommitsLast30Days { get; init; }
    public int CommitsLast365Days { get; init; }
    public int TotalUniqueAuthors { get; init; }
    public int UniqueAuthorsLast7Days { get; init; }
    public int UniqueAuthorsLast30Days { get; init; }
    public int UniqueAuthorsLast365Days { get; init; }
    public double? CoveragePercent { get; init; }
    public double ChurnRiskScore { get; init; }
}
