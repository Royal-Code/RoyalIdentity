namespace Tests.Storage.Support;

/// <summary>
/// Controllable <see cref="TimeProvider"/> for the storage contract suite. Scenarios that depend on time
/// (e.g. session client dedup timestamps) advance it explicitly instead of using the real clock, so the
/// suite never depends on wall-clock timing (plan-data-storage-baseline Fase 3).
/// </summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
	public DateTimeOffset Now { get; set; } = start;

	public override DateTimeOffset GetUtcNow() => Now;

	public void Advance(TimeSpan delta) => Now = Now.Add(delta);
}
