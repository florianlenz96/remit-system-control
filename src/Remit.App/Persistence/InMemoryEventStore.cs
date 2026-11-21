using Remit.Domain;

namespace Remit.App.Persistence;

public sealed class ConcurrencyException(string message) : Exception(message);

/// <summary>
/// Append-only event store held in memory (version 0.0.1). Same contract as the PostgreSQL store
/// that follows (ADR 0004, ADR 0007): streams per aggregate, optimistic concurrency by version.
/// Everything is lost when the process restarts.
/// </summary>
public sealed class InMemoryEventStore
{
    private readonly Dictionary<string, List<IDomainEvent>> _streams = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<IDomainEvent> Load(string streamId)
    {
        lock (_lock)
        {
            return _streams.TryGetValue(streamId, out var events) ? [.. events] : [];
        }
    }

    /// <summary>All streams whose id starts with the prefix, in the order they were created.</summary>
    public IReadOnlyList<IReadOnlyList<IDomainEvent>> LoadAll(string prefix)
    {
        lock (_lock)
        {
            return _streams
                .Where(s => s.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(s => (IReadOnlyList<IDomainEvent>)[.. s.Value])
                .ToList();
        }
    }

    public void Append(string streamId, int expectedVersion, IReadOnlyList<IDomainEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            var current = _streams.TryGetValue(streamId, out var stream) ? stream.Count : 0;
            if (current != expectedVersion)
            {
                throw new ConcurrencyException($"Stream {streamId} is at version {current}, expected {expectedVersion}.");
            }

            if (stream is null)
            {
                stream = [];
                _streams[streamId] = stream;
            }

            stream.AddRange(events);
        }
    }
}
