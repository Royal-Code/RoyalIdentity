using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults.ReplayProtection;

/// <summary>
/// <para>
///     Replay protection scoped to a single process. It is real protection — a replayed handle is refused — but
///     only against replays that reach <b>this</b> instance. A replicated deployment needs a store the replicas
///     share, which is why this one warns on construction and is never a default.
/// </para>
/// <para>
///     Records are kept in a concurrent dictionary and never consulted for expiration on the write path: while a
///     record is retained, a conflict answers replay. Expired records are dropped by periodic pruning, which is
///     memory hygiene and not a condition of the protection.
/// </para>
/// </summary>
public sealed class InMemoryReplayProtectionStore : IReplayProtectionStore, IDisposable
{
    /// <summary>How often expired records are pruned when no interval is given.</summary>
    public static readonly TimeSpan DefaultPruneInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<ReplayHandleKey, DateTimeOffset> handles = new();
    private readonly TimeProvider clock;
    private readonly ITimer pruneTimer;
    private int pruning;

    /// <summary>Creates the store pruning at <see cref="DefaultPruneInterval"/>.</summary>
    public InMemoryReplayProtectionStore(TimeProvider clock, ILogger<InMemoryReplayProtectionStore> logger)
        : this(clock, logger, DefaultPruneInterval)
    {
    }

    /// <summary>Creates the store with an explicit pruning interval.</summary>
    public InMemoryReplayProtectionStore(
        TimeProvider clock,
        ILogger<InMemoryReplayProtectionStore> logger,
        TimeSpan pruneInterval)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pruneInterval, TimeSpan.Zero);

        this.clock = clock;

        logger.LogWarning(
            "In-memory replay protection is active. A replayed handle is only detected by the instance that " +
            "saw it first, so this composition is valid for a single instance only; a replicated deployment " +
            "must declare a store shared by every replica.");

        pruneTimer = clock.CreateTimer(_ => Prune(), null, pruneInterval, pruneInterval);
    }

    /// <summary>Number of records currently retained. Diagnostics only.</summary>
    public int Count => handles.Count;

    /// <inheritdoc />
    public Task<bool> TryAddAsync(
        string realmId,
        string issuer,
        string purpose,
        string handle,
        DateTimeOffset expiration,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ct.ThrowIfCancellationRequested();

        // One operation. No lookup before it, no expiration comparison inside it and no remove-then-add: those
        // are the shapes that let two concurrent callers both believe they were first.
        var added = handles.TryAdd(new ReplayHandleKey(realmId, issuer, purpose, handle), expiration);

        return Task.FromResult(added);
    }

    /// <summary>
    /// Drops every record whose expiration has passed and returns how many were dropped. This is memory hygiene:
    /// it never decides whether a handle is a replay, and skipping it only costs memory. Two executions never
    /// overlap — a call that finds one already running returns immediately.
    /// </summary>
    public int Prune()
    {
        if (Interlocked.CompareExchange(ref pruning, 1, 0) is not 0)
            return 0;

        try
        {
            var now = clock.GetUtcNow();
            var removed = 0;

            foreach (var entry in handles)
            {
                // The pair overload only removes when the value is still the one observed, so a record rewritten
                // between the read and the removal survives.
                if (entry.Value <= now && handles.TryRemove(entry))
                    removed++;
            }

            return removed;
        }
        finally
        {
            Volatile.Write(ref pruning, 0);
        }
    }

    public void Dispose() => pruneTimer.Dispose();

    private readonly record struct ReplayHandleKey(string RealmId, string Issuer, string Purpose, string Handle);
}
