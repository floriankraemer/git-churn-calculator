using GitChurnCalculator.Models;
using Spectre.Console;

namespace GitChurnCalculator.Console.Progress;

public sealed class ChurnProgressReporter
{
    private const int GitProgressTotalSteps = 12;
    private const long RefreshThrottleMilliseconds = 75;

    private readonly IAnsiConsole _console;
    private readonly bool _hasCoverage;
    private readonly object _lock = new();
    private ProgressContext? _context;
    private ProgressTask? _gitTask;
    private ProgressTask? _coverageTask;
    private ProgressTask? _seriesTask;
    private int _lastGitCompletedSteps;
    private int _lastCoverageProcessed = -1;
    private long _lastRefreshTick = long.MinValue;

    public ChurnProgressReporter(IAnsiConsole console, bool hasCoverage)
    {
        _console = console;
        _hasCoverage = hasCoverage;
    }

    public ChurnProgressReporterState State { get; } = new();

    public void Attach(ProgressContext context, int? seriesMax = null)
    {
        lock (_lock)
        {
            _context = context;
            _gitTask = context.AddTask("[green]Collecting git history[/]", maxValue: GitProgressTotalSteps);

            if (_hasCoverage)
                _coverageTask = context.AddTask("[green]Applying coverage[/]", maxValue: 1);

            if (seriesMax is > 0)
            {
                _seriesTask = context.AddTask("[green]Analyzing time series[/]", maxValue: seriesMax.Value);
                State.SeriesMax = seriesMax.Value;
            }
        }
    }

    public void Report(ChurnProgressEvent update)
    {
        if (!ApplyProgress(update))
            return;

        if (_context is null)
        {
            WriteFallbackLine(update);
            return;
        }

        UpdateSpectreTasks(update);
    }

    public void ResetForNextTimeSeriesBucket()
    {
        lock (_lock)
        {
            _lastGitCompletedSteps = 0;
            _lastCoverageProcessed = -1;
            State.GitCompleted = false;
            State.CoverageParseCompleted = false;
            State.CoverageCompleted = false;
            State.GitCompletedSteps = 0;
            State.CoverageProcessed = 0;

            if (_gitTask is not null)
                _gitTask.Value = 0;

            if (_coverageTask is not null)
                _coverageTask.Value = 0;
        }
    }

    public void AdvanceTimeSeries(int completed, int total)
    {
        lock (_lock)
        {
            State.SeriesValue = completed;
            State.SeriesMax = total;
            State.SeriesDescription = $"Analyzing time series ({completed}/{total})";

            if (_seriesTask is not null)
            {
                _seriesTask.MaxValue = total;
                _seriesTask.Value = completed;
                _seriesTask.Description = $"[green]{State.SeriesDescription}[/]";
            }

            _context?.Refresh();
        }
    }

    public void FinalizeSession()
    {
        lock (_lock)
        {
            if (_context is null)
                return;

            FinalizeTask(_gitTask, State.GitDescription ?? "Collecting git history", State.GitTotalSteps, State.GitCompletedSteps);
            FinalizeTask(
                _coverageTask,
                State.CoverageDescription ?? "Applying coverage",
                Math.Max(State.CoverageTotal, 1),
                State.CoverageCompleted ? Math.Max(State.CoverageTotal, 1) : State.CoverageProcessed);

            if (_seriesTask is not null && State.SeriesMax > 0)
            {
                _seriesTask.MaxValue = State.SeriesMax;
                _seriesTask.Value = State.SeriesValue;
                _seriesTask.Description = $"[green]{State.SeriesDescription ?? "Analyzing time series"}[/]";
            }

            _context.Refresh();
        }
    }

    public bool ApplyProgress(ChurnProgressEvent update)
    {
        lock (_lock)
        {
            switch (update.Stage)
            {
                case ChurnProgressStage.TrackedFilesLoaded:
                case ChurnProgressStage.GitQueryCompleted:
                    if (update.CompletedSteps < _lastGitCompletedSteps)
                        return false;

                    _lastGitCompletedSteps = update.CompletedSteps;
                    State.GitDescription = $"Collecting git history - {update.Description}";
                    State.GitCompletedSteps = update.CompletedSteps;
                    State.GitTotalSteps = update.TotalSteps;
                    State.GitCompleted = update.CompletedSteps >= update.TotalSteps;
                    return true;

                case ChurnProgressStage.CoverageParseCompleted:
                    State.CoverageParseCompleted = true;
                    State.CoverageTotal = update.TotalItems ?? State.CoverageTotal;
                    State.CoverageDescription = update.Description;
                    State.CoverageProcessed = 0;
                    return true;

                case ChurnProgressStage.CoverageMappingInProgress:
                    var processed = update.ProcessedItems ?? update.CompletedSteps;
                    var total = update.TotalItems ?? update.TotalSteps;
                    if (processed < _lastCoverageProcessed)
                        return false;

                    _lastCoverageProcessed = processed;
                    State.CoverageTotal = total;
                    State.CoverageProcessed = processed;
                    State.CoverageDescription = $"Mapping coverage ({processed}/{total})";
                    return true;

                case ChurnProgressStage.CoverageMappingCompleted:
                    var finalTotal = update.TotalItems ?? State.CoverageTotal;
                    _lastCoverageProcessed = finalTotal;
                    State.CoverageCompleted = true;
                    State.CoverageTotal = finalTotal;
                    State.CoverageProcessed = update.ProcessedItems ?? finalTotal;
                    State.CoverageDescription = update.Description;
                    return true;

                default:
                    return false;
            }
        }
    }

    private void UpdateSpectreTasks(ChurnProgressEvent update)
    {
        lock (_lock)
        {
            if (_context is null)
                return;

            switch (update.Stage)
            {
                case ChurnProgressStage.TrackedFilesLoaded:
                case ChurnProgressStage.GitQueryCompleted:
                    EnsureGitTask();
                    UpdateTask(_gitTask, State.GitDescription, State.GitTotalSteps, State.GitCompletedSteps);
                    break;

                case ChurnProgressStage.CoverageParseCompleted:
                    EnsureCoverageTask();
                    UpdateTask(_coverageTask, $"[green]{update.Description}[/]", Math.Max(State.CoverageTotal, 1), 0);
                    break;

                case ChurnProgressStage.CoverageMappingInProgress:
                    EnsureCoverageTask();
                    UpdateTask(
                        _coverageTask,
                        $"[green]{State.CoverageDescription}[/]",
                        Math.Max(State.CoverageTotal, 1),
                        State.CoverageProcessed);
                    break;

                case ChurnProgressStage.CoverageMappingCompleted:
                    EnsureCoverageTask();
                    UpdateTask(
                        _coverageTask,
                        $"[green]{State.CoverageDescription}[/]",
                        Math.Max(State.CoverageTotal, 1),
                        Math.Max(State.CoverageTotal, 1));
                    break;
            }

            if (ShouldRefresh(update.Stage))
                _context.Refresh();
        }
    }

    private void EnsureGitTask()
    {
        if (_gitTask is not null || _context is null)
            return;

        _gitTask = _context.AddTask("[green]Collecting git history[/]", maxValue: Math.Max(State.GitTotalSteps, 1));
    }

    private void EnsureCoverageTask()
    {
        if (_coverageTask is not null || _context is null || !_hasCoverage)
            return;

        _coverageTask = _context.AddTask("[green]Applying coverage[/]", maxValue: Math.Max(State.CoverageTotal, 1));
    }

    private static void FinalizeTask(ProgressTask? task, string description, double maxValue, double value)
    {
        if (task is null || maxValue <= 0)
            return;

        task.MaxValue = maxValue;
        task.Description = description.StartsWith('[') ? description : $"[green]{description}[/]";
        task.Value = value;
    }

    private static void UpdateTask(ProgressTask? task, string? description, double maxValue, double value)
    {
        if (task is null)
            return;

        task.MaxValue = maxValue;
        if (!string.IsNullOrWhiteSpace(description))
            task.Description = description;
        task.Value = value;
    }

    private bool ShouldRefresh(ChurnProgressStage stage)
    {
        if (stage != ChurnProgressStage.CoverageMappingInProgress)
        {
            _lastRefreshTick = Environment.TickCount64;
            return true;
        }

        var now = Environment.TickCount64;
        if (now - _lastRefreshTick < RefreshThrottleMilliseconds)
            return false;

        _lastRefreshTick = now;
        return true;
    }

    private void WriteFallbackLine(ChurnProgressEvent update)
    {
        switch (update.Stage)
        {
            case ChurnProgressStage.TrackedFilesLoaded:
                _console.MarkupLine("[grey]Collecting git history...[/]");
                break;

            case ChurnProgressStage.GitQueryCompleted:
                break;

            case ChurnProgressStage.CoverageParseCompleted:
                _console.MarkupLine("[grey]Parsing coverage file...[/]");
                break;

            case ChurnProgressStage.CoverageMappingInProgress when update.ProcessedItems is 0 or null:
                _console.MarkupLine(
                    $"[grey]Mapping coverage to {update.TotalItems ?? update.TotalSteps} tracked path(s)...[/]");
                break;

            case ChurnProgressStage.CoverageMappingCompleted:
            case ChurnProgressStage.CoverageMappingInProgress:
                break;
        }
    }
}
