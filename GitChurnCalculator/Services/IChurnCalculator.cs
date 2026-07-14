using GitChurnCalculator.Models;

namespace GitChurnCalculator.Services;

public interface IChurnCalculator
{
    Task<IReadOnlyList<FileChurnResult>> AnalyzeAsync(
        ChurnAnalysisOptions options,
        IProgress<ChurnProgressEvent>? progress = null,
        CancellationToken ct = default);
}
