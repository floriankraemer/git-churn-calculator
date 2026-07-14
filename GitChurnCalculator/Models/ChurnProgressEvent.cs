namespace GitChurnCalculator.Models;

public enum ChurnProgressStage
{
    TrackedFilesLoaded,
    GitQueryCompleted,
    CoverageParseCompleted,
    CoverageMappingCompleted,
}

public sealed record ChurnProgressEvent(
    ChurnProgressStage Stage,
    string Description,
    int CompletedSteps,
    int TotalSteps);
