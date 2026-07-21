using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RoyalCode.DomainEvents;
using RoyalIdentity.UserAccounts.Infrastructure.Events;

namespace RoyalIdentity.UserAccounts.PostgreSql;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling (migrations) to construct the context without a host:
/// the real app resolves the connection string and the event dispatcher from DI, neither of which is available
/// to the tooling. The dummy connection string here is only used to pick the Npgsql provider for scaffolding —
/// it is never opened.
/// </summary>
public sealed class UserAccountsPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<UserAccountsPostgreSqlDbContext>
{
	/// <inheritdoc />
	public UserAccountsPostgreSqlDbContext CreateDbContext(string[] args)
	{
		var options = new DbContextOptionsBuilder<UserAccountsPostgreSqlDbContext>()
			.UseNpgsql("Host=design-time;Database=design-time;Username=design-time;Password=design-time")
			.Options;

		return new UserAccountsPostgreSqlDbContext(options, NoopDomainEventDispatcher.Instance);
	}

	private sealed class NoopDomainEventDispatcher : IDomainEventDispatcher
	{
		public static readonly NoopDomainEventDispatcher Instance = new();

		public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default)
			=> Task.CompletedTask;
	}
}
