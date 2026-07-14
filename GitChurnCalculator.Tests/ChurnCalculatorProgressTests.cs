using GitChurnCalculator.Models;
using GitChurnCalculator.Services;
using NSubstitute;
using Xunit;

namespace GitChurnCalculator.Tests;

public class ChurnCalculatorProgressTests
{
    private const int GitProgressTotalSteps = 12;

    [Fact]
    public async Task AnalyzeAsync_WithoutCoverage_ReportsGitProgressEvents()
    {
        var gitProvider = CreateGitProvider();
        var coverageParser = Substitute.For<ICoverageParser>();
        var calculator = new ChurnCalculator(gitProvider, coverageParser);
        var events = new List<ChurnProgressEvent>();
        var progress = new Progress<ChurnProgressEvent>(events.Add);

        await calculator.AnalyzeAsync(
            new ChurnAnalysisOptions { RepositoryPath = "/fake/repo" },
            progress);

        Assert.Equal(1, events.Count(e => e.Stage == ChurnProgressStage.TrackedFilesLoaded));
        Assert.Equal(11, events.Count(e => e.Stage == ChurnProgressStage.GitQueryCompleted));
        Assert.Empty(events.Where(e => e.Stage is ChurnProgressStage.CoverageParseCompleted
            or ChurnProgressStage.CoverageMappingCompleted));

        var trackedFilesEvent = events.Single(e => e.Stage == ChurnProgressStage.TrackedFilesLoaded);
        Assert.Equal(1, trackedFilesEvent.CompletedSteps);
        Assert.Equal(GitProgressTotalSteps, trackedFilesEvent.TotalSteps);

        var lastGitEvent = events.Last(e => e.Stage == ChurnProgressStage.GitQueryCompleted);
        Assert.Equal(GitProgressTotalSteps, lastGitEvent.CompletedSteps);
        Assert.Equal(GitProgressTotalSteps, lastGitEvent.TotalSteps);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCoverage_ReportsCoverageProgressEvents()
    {
        var gitProvider = CreateGitProvider();
        var coverageParser = Substitute.For<ICoverageParser>();
        coverageParser.Parse("/fake/coverage.xml").Returns(new Dictionary<string, double>
        {
            ["file.cs"] = 80.0,
        });
        coverageParser.MapToTrackedFiles(Arg.Any<Dictionary<string, double>>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["file.cs"] = 80.0,
            });

        var calculator = new ChurnCalculator(gitProvider, coverageParser);
        var events = new List<ChurnProgressEvent>();
        var progress = new Progress<ChurnProgressEvent>(events.Add);

        await calculator.AnalyzeAsync(
            new ChurnAnalysisOptions
            {
                RepositoryPath = "/fake/repo",
                CoverageFilePath = "/fake/coverage.xml",
            },
            progress);

        Assert.Equal(1, events.Count(e => e.Stage == ChurnProgressStage.CoverageParseCompleted));
        Assert.Equal(1, events.Count(e => e.Stage == ChurnProgressStage.CoverageMappingCompleted));
        Assert.Contains(events, e => e.Stage == ChurnProgressStage.CoverageMappingInProgress);

        var parseEvent = events.Single(e => e.Stage == ChurnProgressStage.CoverageParseCompleted);
        Assert.Equal(1, parseEvent.CompletedSteps);
        Assert.Equal(2, parseEvent.TotalSteps);

        var mapEvent = events.Single(e => e.Stage == ChurnProgressStage.CoverageMappingCompleted);
        Assert.Equal(2, mapEvent.CompletedSteps);
        Assert.Equal(2, mapEvent.TotalSteps);
    }

    private static IGitDataProvider CreateGitProvider()
    {
        var gitProvider = Substitute.For<IGitDataProvider>();
        var repoPath = "/fake/repo";
        var now = DateTime.UtcNow;

        gitProvider.GetTrackedFilesAsync(repoPath, Arg.Any<CancellationToken>())
            .Returns(new List<string> { "file.cs" });

        var commitCounts = new Dictionary<string, int> { ["file.cs"] = 5 };
        gitProvider.GetCommitCountsAsync(repoPath, Arg.Any<CancellationToken>()).Returns(commitCounts);

        var dates = new Dictionary<string, DateTime> { ["file.cs"] = now.AddDays(-30) };
        gitProvider.GetFirstCommitDatesAsync(repoPath, Arg.Any<CancellationToken>()).Returns(dates);
        gitProvider.GetLastCommitDatesAsync(repoPath, Arg.Any<CancellationToken>()).Returns(dates);

        var authors = new Dictionary<string, int> { ["file.cs"] = 2 };
        gitProvider.GetUniqueAuthorCountsAsync(repoPath, Arg.Any<CancellationToken>()).Returns(authors);

        gitProvider.GetLineChangeTotalsAsync(repoPath, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, LineChangeTotals>(StringComparer.Ordinal)
            {
                ["file.cs"] = new LineChangeTotals(10, 5),
            });

        var empty = new Dictionary<string, int>();
        gitProvider.GetCommitCountsSinceAsync(repoPath, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(empty);
        gitProvider.GetUniqueAuthorCountsSinceAsync(repoPath, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(empty);

        return gitProvider;
    }
}
