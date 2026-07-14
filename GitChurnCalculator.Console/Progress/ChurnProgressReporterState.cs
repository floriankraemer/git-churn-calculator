namespace GitChurnCalculator.Console.Progress;

public sealed class ChurnProgressReporterState
{
    public string? GitDescription { get; internal set; }
    public int GitCompletedSteps { get; internal set; }
    public int GitTotalSteps { get; internal set; }
    public bool GitCompleted { get; internal set; }

    public string? CoverageDescription { get; internal set; }
    public int CoverageProcessed { get; internal set; }
    public int CoverageTotal { get; internal set; }
    public bool CoverageParseCompleted { get; internal set; }
    public bool CoverageCompleted { get; internal set; }

    public string? SeriesDescription { get; internal set; }
    public int SeriesValue { get; internal set; }
    public int SeriesMax { get; internal set; }
}
