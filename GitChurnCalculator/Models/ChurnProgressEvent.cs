namespace GitChurnCalculator.Models;

public enum ChurnProgressStage
{
    TrackedFilesLoaded,
    GitQueryCompleted,
    CoverageParseCompleted,
    CoverageMappingInProgress,
    CoverageMappingCompleted,
}

public sealed record ChurnProgressEvent(
    ChurnProgressStage Stage,
    string Description,
    int CompletedSteps,
    int TotalSteps,
    int? ProcessedItems = null,
    int? TotalItems = null);
