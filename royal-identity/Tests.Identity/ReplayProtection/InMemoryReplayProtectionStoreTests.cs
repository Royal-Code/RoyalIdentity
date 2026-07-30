using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contracts.Defaults.ReplayProtection;

namespace Tests.Identity.ReplayProtection;

/// <summary>
/// Exercises the single-process backing of <c>IReplayProtectionStore</c>: the atomic registration, the four
/// dimensions of isolation, and the pruning that is memory hygiene rather than a condition of the protection
/// (plan-replay-protection DF8/DF13/DF20).
/// </summary>
public class InMemoryReplayProtectionStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    private const string Purpose = "PrivateKeyJwtSecretEvaluator";

    [Fact]
    public async Task FirstPresentation_IsRegistered_AndTheSecondIsReplay()
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);

        Assert.True(await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddMinutes(10), default));
        Assert.False(await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddMinutes(10), default));
    }

    [Theory]
    // Each row changes exactly one dimension of the identity; none of them may see the other's record.
    [InlineData("realm-b", "client-a", Purpose, "jti-1")]
    [InlineData("realm-a", "client-b", Purpose, "jti-1")]
    [InlineData("realm-a", "client-a", "OtherPurpose", "jti-1")]
    [InlineData("realm-a", "client-a", Purpose, "jti-2")]
    public async Task RecordsOfDifferentIdentities_DoNotInterfere(
        string realmId, string issuer, string purpose, string handle)
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);

        Assert.True(await store.TryAddAsync("realm-a", "client-a", Purpose, "jti-1", Now.AddMinutes(10), default));
        Assert.True(await store.TryAddAsync(realmId, issuer, purpose, handle, Now.AddMinutes(10), default));
    }

    // DF8: the write path never consults the expiration, so a record kept past its expiration still answers
    // replay. Losing this is the difference between "protection independent of pruning" and "protection that
    // silently lapses whenever pruning is late".
    [Fact]
    public async Task ExpiredButNotYetPrunedRecord_StillAnswersReplay()
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);

        Assert.True(await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddMinutes(10), default));

        clock.SetUtcNow(Now.AddHours(1));

        Assert.False(await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddHours(2), default));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task Prune_RemovesOnlyRecordsWhoseExpirationHasPassed()
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);

        await store.TryAddAsync("realm", "client", Purpose, "past", Now.AddMinutes(-1), default);
        await store.TryAddAsync("realm", "client", Purpose, "exactly-now", Now, default);
        await store.TryAddAsync("realm", "client", Purpose, "future", Now.AddSeconds(1), default);

        // The boundary is inclusive: a record expiring exactly now can no longer protect anything.
        Assert.Equal(2, store.Prune());
        Assert.Equal(1, store.Count);

        Assert.False(await store.TryAddAsync("realm", "client", Purpose, "future", Now.AddMinutes(10), default));
        Assert.True(await store.TryAddAsync("realm", "client", Purpose, "past", Now.AddMinutes(10), default));
    }

    [Fact]
    public async Task PruneTimer_IsCreatedByTheInjectedClock_AndPrunesWhenItFires()
    {
        var clock = new TestTimeProvider(Now);
        using var store = new InMemoryReplayProtectionStore(
            clock, NullLogger<InMemoryReplayProtectionStore>.Instance, TimeSpan.FromMinutes(5));

        var timer = Assert.Single(clock.Timers);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.DueTime);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Period);

        await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddMinutes(-1), default);

        timer.Fire();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Dispose_StopsThePruneTimer()
    {
        var clock = new TestTimeProvider(Now);
        var store = Create(clock);
        var timer = Assert.Single(clock.Timers);

        Assert.False(timer.Disposed);

        store.Dispose();

        Assert.True(timer.Disposed);
    }

    // Reentrancy through the clock is the deterministic way to prove the gate: Prune reads the clock inside the
    // guarded region, so the second call happens while the first is demonstrably running.
    [Fact]
    public async Task Prune_DoesNotOverlapWithItself()
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);
        await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddMinutes(-1), default);

        int? reentrantResult = null;
        var countDuringPrune = -1;
        clock.OnGetUtcNow = () =>
        {
            if (reentrantResult.HasValue)
                return;

            countDuringPrune = store.Count;
            reentrantResult = store.Prune();
        };

        Assert.Equal(1, store.Prune());

        Assert.Equal(0, reentrantResult);
        Assert.Equal(1, countDuringPrune);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task ConcurrentPresentationsOfTheSameHandle_ProduceExactlyOneWinner()
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callers = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await store.TryAddAsync("realm", "client", Purpose, "jti-1", Now.AddMinutes(10), default);
        })).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(callers);

        Assert.Equal(1, results.Count(added => added));
    }

    [Theory]
    [InlineData("", "client", Purpose, "jti")]
    [InlineData("realm", "", Purpose, "jti")]
    [InlineData("realm", "client", "", "jti")]
    [InlineData("realm", "client", Purpose, "")]
    public async Task IncompleteIdentity_IsRejected(
        string realmId, string issuer, string purpose, string handle)
    {
        var clock = new TestTimeProvider(Now);
        using var store = Create(clock);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TryAddAsync(realmId, issuer, purpose, handle, Now.AddMinutes(10), default));
    }

    private static InMemoryReplayProtectionStore Create(TimeProvider clock)
        => new(clock, NullLogger<InMemoryReplayProtectionStore>.Instance);

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = utcNow;

        public List<RecordingTimer> Timers { get; } = [];

        public Action? OnGetUtcNow { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            OnGetUtcNow?.Invoke();
            return utcNow;
        }

        public void SetUtcNow(DateTimeOffset value) => utcNow = value;

        public override ITimer CreateTimer(
            TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new RecordingTimer(callback, state, dueTime, period);
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class RecordingTimer(
        TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        public TimeSpan DueTime { get; private set; } = dueTime;

        public TimeSpan Period { get; private set; } = period;

        public bool Disposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            Period = period;
            return true;
        }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
