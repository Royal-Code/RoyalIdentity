using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

namespace Tests.Storage.Operational;

/// <summary>
/// The hosted cleanup worker (plan Fase 6, DF17) — the only executor of <see cref="CleanupExecutionMode.Hosted"/>.
/// It is driven through the public registration seam, so what these scenarios exercise is the composition a
/// deployment actually gets: a pass per tick, a fresh DI scope per pass, the configured batch and clock, and a
/// loop that survives a failing pass and stops on cancellation.
/// </summary>
public class OperationalCleanupWorkerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Ticks fast enough to keep the suite quick; every assertion waits on a signal, never on a sleep.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    // A pass per tick, with the batch and the instant the composition configured.
    [Fact]
    public async Task Worker_RunsAPassPerTick_WithTheConfiguredBatchAndClock()
    {
        await using var host = WorkerHost.Create(batchSize: 37);

        await host.StartAsync();
        await host.Recorder.WaitForPassesAsync(2);
        await host.StopAsync();

        Assert.All(host.Recorder.Passes, pass => Assert.Equal(37, pass.BatchSize));
        Assert.All(host.Recorder.Passes, pass => Assert.Equal(FixedNow.UtcDateTime, pass.Now));
        Assert.All(host.Recorder.Passes, pass => Assert.Equal(DateTimeKind.Utc, pass.Now.Kind));
    }

    // A scope per pass: the maintenance and its DbContext are scoped, exactly like in a request.
    [Fact]
    public async Task Worker_CreatesOneScopePerPass()
    {
        await using var host = WorkerHost.Create();

        await host.StartAsync();
        await host.Recorder.WaitForPassesAsync(3);
        await host.StopAsync();

        var scopes = host.Recorder.Passes.Select(pass => pass.Scope).ToList();

        Assert.Equal(scopes.Count, scopes.Distinct().Count());
    }

    // A failing pass is logged and the loop continues: cleanup is idempotent, so the next pass picks up what the
    // failed one left.
    [Fact]
    public async Task Worker_ContinuesAfterAFailingPass()
    {
        await using var host = WorkerHost.Create(failFirstPasses: 2);

        await host.StartAsync();
        await host.Recorder.WaitForPassesAsync(4);
        await host.StopAsync();

        Assert.Equal(2, host.Recorder.Failures);
        Assert.True(host.Recorder.Passes.Count >= 4);
    }

    // Stopping the host cancels the loop and no further pass runs.
    [Fact]
    public async Task Worker_StopsOnCancellation_AndRunsNoFurtherPass()
    {
        await using var host = WorkerHost.Create();

        await host.StartAsync();
        await host.Recorder.WaitForPassesAsync(1);
        await host.StopAsync();

        var afterStop = host.Recorder.Passes.Count;
        // Several intervals' worth of time with the worker stopped.
        await Task.Delay(Interval * 10);

        Assert.Equal(afterStop, host.Recorder.Passes.Count);
        // The token the worker hands to the maintenance is its own stopping token, not a dead one.
        Assert.All(host.Recorder.Passes, pass => Assert.True(pass.TokenCanBeCanceled));
    }

    /// <summary>What one cleanup pass observed, so the scenarios can assert on it afterwards.</summary>
    private sealed record RecordedPass(DateTime Now, int BatchSize, object Scope, bool TokenCanBeCanceled);

    /// <summary>Singleton recorder: collects passes and signals when the requested count is reached.</summary>
    private sealed class PassRecorder(int failFirstPasses)
    {
        private readonly Lock gate = new();
        private readonly List<RecordedPass> passes = [];
        private TaskCompletionSource wanted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int wantedCount = int.MaxValue;
        private int failures;

        public IReadOnlyList<RecordedPass> Passes
        {
            get
            {
                lock (gate)
                    return [.. passes];
            }
        }

        public int Failures => Volatile.Read(ref failures);

        /// <summary>Records the pass and reports whether this one must fail.</summary>
        public bool Record(RecordedPass pass)
        {
            lock (gate)
            {
                passes.Add(pass);

                if (passes.Count >= wantedCount)
                    wanted.TrySetResult();

                if (passes.Count <= failFirstPasses)
                {
                    Interlocked.Increment(ref failures);
                    return true;
                }

                return false;
            }
        }

        public async Task WaitForPassesAsync(int count)
        {
            lock (gate)
            {
                wantedCount = count;
                wanted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                if (passes.Count >= count)
                    wanted.TrySetResult();
            }

            await wanted.Task.WaitAsync(Timeout);
        }
    }

    /// <summary>Scoped marker whose identity is the scope's: distinct instances mean distinct scopes.</summary>
    private sealed class ScopeMarker;

    private sealed class SpyMaintenance(PassRecorder recorder, ScopeMarker scope) : IOperationalMaintenance
    {
        public Task<OperationalCleanupReport> CleanupAsync(DateTime now, int batchSize, CancellationToken ct = default)
        {
            var mustFail = recorder.Record(new RecordedPass(now, batchSize, scope, ct.CanBeCanceled));

            return mustFail
                ? Task.FromException<OperationalCleanupReport>(new InvalidOperationException("staged pass failure"))
                : Task.FromResult(new OperationalCleanupReport(1, 0, 0, 0, 0, 0));
        }

        public Task<OperationalPurgeReport> PurgeRealmAsync(string realmId, CancellationToken ct = default)
            => throw new NotSupportedException("The worker never purges.");
    }

    /// <summary>
    /// A clock fixed in time for the assertions, but whose timers are real — the worker's
    /// <c>PeriodicTimer</c> must actually tick, while the instant it passes to the maintenance stays
    /// deterministic.
    /// </summary>
    private sealed class FixedInstantRealTimerClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => FixedNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => System.CreateTimer(callback, state, dueTime, period);
    }

    /// <summary>The worker as a composition gets it: registered by the public seam, started as a hosted service.</summary>
    private sealed class WorkerHost : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly IHostedService worker;
        private bool stopped;

        private WorkerHost(ServiceProvider services, IHostedService worker, PassRecorder recorder)
        {
            this.services = services;
            this.worker = worker;
            Recorder = recorder;
        }

        public PassRecorder Recorder { get; }

        public static WorkerHost Create(int batchSize = 500, int failFirstPasses = 0)
        {
            var recorder = new PassRecorder(failFirstPasses);
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(new FixedInstantRealTimerClock());
            collection.AddSingleton(recorder);
            collection.AddScoped<ScopeMarker>();
            collection.AddScoped<IOperationalMaintenance, SpyMaintenance>();
            collection.AddEntityFrameworkOperationalCleanup(cleanup =>
            {
                cleanup.Mode = CleanupExecutionMode.Hosted;
                cleanup.Interval = Interval;
                cleanup.BatchSize = batchSize;
            });

            var services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            return new WorkerHost(services, services.GetServices<IHostedService>().Single(), recorder);
        }

        public Task StartAsync() => worker.StartAsync(CancellationToken.None);

        public async Task StopAsync()
        {
            if (stopped)
                return;

            stopped = true;
            await worker.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            await services.DisposeAsync();
        }
    }
}
