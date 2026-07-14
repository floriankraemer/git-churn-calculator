using GitChurnCalculator.Models;
using Xunit;

namespace GitChurnCalculator.Tests;

public class SynchronizedProgressTests
{
    [Fact]
    public void Report_ConcurrentCalls_InvokesHandlerForEveryEvent()
    {
        var count = 0;
        var progress = new SynchronizedProgress<int>(_ => Interlocked.Increment(ref count));

        Parallel.For(0, 100, i => progress.Report(i));

        Assert.Equal(100, count);
    }
}
