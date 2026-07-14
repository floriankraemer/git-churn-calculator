using GitChurnCalculator.Models;
using GitChurnCalculator.Services;
using Spectre.Console;

namespace GitChurnCalculator.Console.Cli;

internal static class SpectreChurnProgress
{
    private const int GitProgressTotalSteps = 13;
    private const int CoverageProgressTotalSteps = 2;

    internal static bool CanShowLiveProgress =>
        !global::System.Console.IsErrorRedirected;

    private static IAnsiConsole CreateStdErrConsole()
    {
        var redirected = global::System.Console.IsErrorRedirected;
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(global::System.Console.Error),
            Ansi = redirected ? AnsiSupport.No : AnsiSupport.Detect,
            Interactive = redirected ? InteractionSupport.No : InteractionSupport.Detect,
        });
    }

    public static async Task<IReadOnlyList<FileChurnResult>> RunSnapshotAsync(
        IChurnCalculator calculator,
        ChurnAnalysisOptions options,
        bool hasCoverage,
        CancellationToken ct = default)
    {
        if (!CanShowLiveProgress)
            return await calculator.AnalyzeAsync(options, progress: null, ct);

        IReadOnlyList<FileChurnResult>? results = null;
        var console = CreateStdErrConsole();

        await console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .StartAsync(async ctx =>
            {
                var gitTask = ctx.AddTask("[green]Collecting git history[/]", maxValue: GitProgressTotalSteps);
                ProgressTask? coverageTask = hasCoverage
                    ? ctx.AddTask("[green]Applying coverage[/]", maxValue: CoverageProgressTotalSteps)
                    : null;

                var progress = CreateHandler(gitTask, coverageTask);
                results = await calculator.AnalyzeAsync(options, progress, ct);
            });

        return results!;
    }

    public static async Task<List<TimeSeriesPoint>> RunTimeSeriesAsync(
        IChurnCalculator calculator,
        DirectoryInfo repo,
        FileInfo? coverage,
        string? include,
        string? exclude,
        IReadOnlyList<DateTime> bucketEnds,
        CancellationToken ct = default)
    {
        if (!CanShowLiveProgress)
            return await RunTimeSeriesWithoutLiveProgressAsync(
                calculator, repo, coverage, include, exclude, bucketEnds, ct);

        var points = new List<TimeSeriesPoint>(bucketEnds.Count);
        var hasCoverage = coverage is not null;
        var console = CreateStdErrConsole();

        await console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .StartAsync(async ctx =>
            {
                var seriesTask = ctx.AddTask(
                    "[green]Analyzing time series[/]",
                    maxValue: bucketEnds.Count);
                var gitTask = ctx.AddTask("[green]Collecting git history[/]", maxValue: GitProgressTotalSteps);
                ProgressTask? coverageTask = hasCoverage
                    ? ctx.AddTask("[green]Applying coverage[/]", maxValue: CoverageProgressTotalSteps)
                    : null;

                foreach (var asOf in bucketEnds)
                {
                    gitTask.Value = 0;
                    if (coverageTask is not null)
                        coverageTask.Value = 0;

                    var options = new ChurnAnalysisOptions
                    {
                        RepositoryPath = repo.FullName,
                        CoverageFilePath = coverage?.FullName,
                        IncludePattern = include,
                        ExcludePattern = exclude,
                        AsOf = asOf,
                    };

                    var progress = CreateHandler(gitTask, coverageTask);
                    var results = await calculator.AnalyzeAsync(options, progress, ct);
                    points.Add(new TimeSeriesPoint { AsOf = asOf, Files = results });
                    seriesTask.Increment(1);
                    seriesTask.Description = $"[green]Analyzing time series[/] ({seriesTask.Value}/{bucketEnds.Count})";
                }
            });

        return points;
    }

    private static async Task<List<TimeSeriesPoint>> RunTimeSeriesWithoutLiveProgressAsync(
        IChurnCalculator calculator,
        DirectoryInfo repo,
        FileInfo? coverage,
        string? include,
        string? exclude,
        IReadOnlyList<DateTime> bucketEnds,
        CancellationToken ct)
    {
        var points = new List<TimeSeriesPoint>(bucketEnds.Count);
        foreach (var asOf in bucketEnds)
        {
            var options = new ChurnAnalysisOptions
            {
                RepositoryPath = repo.FullName,
                CoverageFilePath = coverage?.FullName,
                IncludePattern = include,
                ExcludePattern = exclude,
                AsOf = asOf,
            };
            var results = await calculator.AnalyzeAsync(options, progress: null, ct);
            points.Add(new TimeSeriesPoint { AsOf = asOf, Files = results });
        }

        return points;
    }

    private static IProgress<ChurnProgressEvent> CreateHandler(
        ProgressTask gitTask,
        ProgressTask? coverageTask) =>
        new SynchronizedProgress<ChurnProgressEvent>(e =>
        {
            switch (e.Stage)
            {
                case ChurnProgressStage.TrackedFilesLoaded:
                case ChurnProgressStage.GitQueryCompleted:
                    gitTask.Value = e.CompletedSteps;
                    gitTask.Description = $"[green]Collecting git history[/] - {e.Description}";
                    break;

                case ChurnProgressStage.CoverageParseCompleted:
                case ChurnProgressStage.CoverageMappingCompleted:
                    if (coverageTask is not null)
                    {
                        coverageTask.Value = e.CompletedSteps;
                        coverageTask.Description = $"[green]Applying coverage[/] - {e.Description}";
                    }
                    break;
            }
        });
}
