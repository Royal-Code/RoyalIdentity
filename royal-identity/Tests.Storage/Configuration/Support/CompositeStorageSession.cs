using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Storage.Configuration.Support;

/// <summary>Shared counter so a test can assert a scoped dependency was released exactly once per session.</summary>
internal sealed class ScopedDisposalTracker
{
	public int DisposedCount;
}

/// <summary>A scoped, disposable dependency that reports its disposal to the shared tracker.</summary>
internal sealed class ScopedProbe(ScopedDisposalTracker tracker) : IDisposable
{
	public void Dispose() => Interlocked.Increment(ref tracker.DisposedCount);
}

/// <summary>
/// Test-only <see cref="IStorageProvider"/> modelling the Plano-3 lifecycle (plan DF6/DF20): each session owns
/// a fresh DI scope combining the Configuration EF context and the Operational in-memory storage. It exists
/// only to prove scope/disposal semantics before the production composition is built; it is never a public
/// registration.
/// </summary>
internal sealed class CompositeStorageProvider(IServiceScopeFactory scopeFactory) : IStorageProvider
{
	public IStorageSession CreateSession() => new CompositeStorageSession(scopeFactory.CreateScope());
}

internal sealed class CompositeStorageSession : IStorageSession
{
	private readonly IServiceScope scope;

	public CompositeStorageSession(IServiceScope scope)
	{
		this.scope = scope;
		// Configuration (EF) is a scoped dependency owned by this session; the scoped probe stands in for any
		// other scoped resource whose release proves the scope was disposed.
		ConfigurationDbContext = scope.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>();
		_ = scope.ServiceProvider.GetRequiredService<ScopedProbe>();
	}

	public ConfigurationSqliteDbContext ConfigurationDbContext { get; }

	/// <summary>The Operational storage (in-memory in this composite) resolved within the session scope.</summary>
	public IStorage GetStorage() => scope.ServiceProvider.GetRequiredService<IStorage>();

	public void Dispose() => scope.Dispose();
}
