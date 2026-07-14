using GitChurnCalculator.Models;
using GitChurnCalculator.Services;
using Spectre.Console;

namespace GitChurnCalculator.Console.Progress;

internal static class SpectreChurnProgressSession
{
    private static IAnsiConsole CreateStdErrConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(global::System.Console.Error),
            Ansi = AnsiSupport.Detect,
            Interactive = InteractionSupport.Detect,
        });

    public static Task<IReadOnlyList<FileChurnResult>> RunSnapshotAsync(
        IChurnCalculator calculator,
        ChurnAnalysisOptions options,
        bool hasCoverage,
        CancellationToken ct = default) =>
        RunAsync(
            hasCoverage,
            seriesMax: null,
            async (reporter, progress) =>
                await calculator.AnalyzeAsync(options, progress, ct));

    public static async Task<List<TimeSeriesPoint>> RunTimeSeriesAsync(
        IChurnCalculator calculator,
        DirectoryInfo repo,
        FileInfo? coverage,
        string? include,
        string? exclude,
        IReadOnlyList<DateTime> bucketEnds,
        CancellationToken ct = default)
    {
        var points = new List<TimeSeriesPoint>(bucketEnds.Count);
        var hasCoverage = coverage is not null;
        var completed = 0;

        await RunVoidAsync(
            hasCoverage,
            bucketEnds.Count,
            async (reporter, progress) =>
            {
                foreach (var asOf in bucketEnds)
                {
                    reporter.ResetForNextTimeSeriesBucket();

                    var options = new ChurnAnalysisOptions
                    {
                        RepositoryPath = repo.FullName,
                        CoverageFilePath = coverage?.FullName,
                        IncludePattern = include,
                        ExcludePattern = exclude,
                        AsOf = asOf,
                    };

                    var results = await calculator.AnalyzeAsync(options, progress, ct);
                    points.Add(new TimeSeriesPoint { AsOf = asOf, Files = results });
                    completed++;
                    reporter.AdvanceTimeSeries(completed, bucketEnds.Count);
                }
            });

        return points;
    }

    private static async Task<T> RunAsync<T>(
        bool hasCoverage,
        int? seriesMax,
        Func<ChurnProgressReporter, IProgress<ChurnProgressEvent>, Task<T>> action)
    {
        var console = CreateStdErrConsole();

        if (!console.Profile.Capabilities.Interactive)
        {
            var reporter = new ChurnProgressReporter(console, hasCoverage);
            var progress = new SynchronousChurnProgress(reporter);
            var silentResult = await action(reporter, progress);
            reporter.FinalizeSession();
            return silentResult;
        }

        T? interactiveResult = default;
        await console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var reporter = new ChurnProgressReporter(console, hasCoverage);
                reporter.Attach(ctx, seriesMax);
                var progress = new SynchronousChurnProgress(reporter);
                interactiveResult = await action(reporter, progress);
                reporter.FinalizeSession();
            });

        return interactiveResult!;
    }

    private static async Task RunVoidAsync(
        bool hasCoverage,
        int? seriesMax,
        Func<ChurnProgressReporter, IProgress<ChurnProgressEvent>, Task> action)
    {
        var console = CreateStdErrConsole();

        if (!console.Profile.Capabilities.Interactive)
        {
            var reporter = new ChurnProgressReporter(console, hasCoverage);
            var progress = new SynchronousChurnProgress(reporter);
            await action(reporter, progress);
            reporter.FinalizeSession();
            return;
        }

        await console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var reporter = new ChurnProgressReporter(console, hasCoverage);
                reporter.Attach(ctx, seriesMax);
                var progress = new SynchronousChurnProgress(reporter);
                await action(reporter, progress);
                reporter.FinalizeSession();
            });
    }
}
