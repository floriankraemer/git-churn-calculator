using GitChurnCalculator.Console.Progress;
using GitChurnCalculator.Models;
using Spectre.Console;
using Xunit;

namespace GitChurnCalculator.Console.Tests;

public class ChurnProgressReporterTests
{
    [Fact]
    public void ApplyProgress_GitQueryCompleted_SetsDescriptionWithStepCounts()
    {
        var reporter = new ChurnProgressReporter(CreateConsole(), hasCoverage: false);

        reporter.ApplyProgress(new ChurnProgressEvent(
            ChurnProgressStage.GitQueryCompleted,
            "Commit counts",
            5,
            12));

        Assert.Equal("Collecting git history - Commit counts", reporter.State.GitDescription);
        Assert.Equal(5, reporter.State.GitCompletedSteps);
        Assert.Equal(12, reporter.State.GitTotalSteps);
    }

    [Fact]
    public void ApplyProgress_GitQueryCompleted_IgnoresOutOfOrderUpdates()
    {
        var reporter = new ChurnProgressReporter(CreateConsole(), hasCoverage: false);

        Assert.True(reporter.ApplyProgress(new ChurnProgressEvent(
            ChurnProgressStage.GitQueryCompleted,
            "Later",
            8,
            12)));
        Assert.False(reporter.ApplyProgress(new ChurnProgressEvent(
            ChurnProgressStage.GitQueryCompleted,
            "Earlier",
            3,
            12)));

        Assert.Equal(8, reporter.State.GitCompletedSteps);
    }

    [Fact]
    public void ApplyProgress_CoverageMappingInProgress_SetsDescriptionWithCounts()
    {
        var reporter = new ChurnProgressReporter(CreateConsole(), hasCoverage: true);

        reporter.ApplyProgress(new ChurnProgressEvent(
            ChurnProgressStage.CoverageMappingInProgress,
            "Mapping coverage to tracked files",
            200,
            500,
            200,
            500));

        Assert.Equal("Mapping coverage (200/500)", reporter.State.CoverageDescription);
        Assert.Equal(200, reporter.State.CoverageProcessed);
        Assert.Equal(500, reporter.State.CoverageTotal);
    }

    [Fact]
    public void Report_ConcurrentGitQueryEvents_DoesNotThrow()
    {
        var reporter = new ChurnProgressReporter(CreateConsole(), hasCoverage: false);

        Parallel.For(1, 12, step =>
        {
            reporter.Report(new ChurnProgressEvent(
                ChurnProgressStage.GitQueryCompleted,
                $"Step {step}",
                step,
                12));
        });

        Assert.Equal(11, reporter.State.GitCompletedSteps);
    }

    [Fact]
    public void Report_WithoutAttachedContext_WritesFallbackLines()
    {
        var writer = new StringWriter();
        var reporter = new ChurnProgressReporter(CreateConsole(writer), hasCoverage: true);

        reporter.Report(new ChurnProgressEvent(
            ChurnProgressStage.TrackedFilesLoaded,
            "Tracked files loaded",
            1,
            12));
        reporter.Report(new ChurnProgressEvent(
            ChurnProgressStage.CoverageParseCompleted,
            "Coverage parsed",
            1,
            2,
            TotalItems: 120));

        var output = writer.ToString();
        Assert.Contains("Collecting git history", output);
        Assert.Contains("Parsing coverage file", output);
    }

    private static IAnsiConsole CreateConsole(StringWriter? writer = null)
    {
        writer ??= new StringWriter();
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
        });
    }
}
