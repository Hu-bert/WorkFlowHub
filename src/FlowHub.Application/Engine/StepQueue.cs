using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FlowHub.Application.Engine;

/// <summary>Kolejka zatwierdzonych kroków do uruchomienia w tle + możliwość przerwania działającego.</summary>
public sealed class StepQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public void Enqueue(Guid stepRunId) => _channel.Writer.TryWrite(stepRunId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public bool TryDequeue(out Guid stepRunId) => _channel.Reader.TryRead(out stepRunId);

    /// <summary>Rejestruje działający krok; zwrócony token zostanie anulowany przez <see cref="Cancel"/>.</summary>
    public CancellationTokenSource Track(Guid stepRunId, CancellationToken appStopping)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(appStopping);
        _running[stepRunId] = source;
        return source;
    }

    public void Untrack(Guid stepRunId) => _running.TryRemove(stepRunId, out _);

    public bool Cancel(Guid stepRunId)
    {
        if (!_running.TryGetValue(stepRunId, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }
}
