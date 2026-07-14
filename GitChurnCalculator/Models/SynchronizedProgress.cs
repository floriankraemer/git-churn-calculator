namespace GitChurnCalculator.Models;

/// <summary>
/// Serializes progress callbacks so handlers that are not thread-safe
/// (e.g. Spectre.Console progress tasks) never receive concurrent updates.
/// </summary>
public sealed class SynchronizedProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;
    private readonly object _lock = new();

    public SynchronizedProgress(Action<T> handler) => _handler = handler;

    public void Report(T value)
    {
        lock (_lock)
            _handler(value);
    }

    public static IProgress<T>? Wrap(IProgress<T>? progress)
    {
        if (progress is null)
            return null;

        var inner = progress;
        return new SynchronizedProgress<T>(e => inner.Report(e));
    }
}
