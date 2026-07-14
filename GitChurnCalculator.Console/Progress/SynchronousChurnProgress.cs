using GitChurnCalculator.Models;

namespace GitChurnCalculator.Console.Progress;

/// <summary>
/// Synchronous <see cref="IProgress{T}"/> adapter that invokes the reporter on the calling thread,
/// avoiding the thread-pool dispatch of <see cref="Progress{T}"/>.
/// </summary>
internal sealed class SynchronousChurnProgress(ChurnProgressReporter reporter) : IProgress<ChurnProgressEvent>
{
    public void Report(ChurnProgressEvent value) => reporter.Report(value);
}
