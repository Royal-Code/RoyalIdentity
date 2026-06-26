using Microsoft.EntityFrameworkCore;
using RoyalCode.DomainEvents;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Events;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data;

/// <summary>
/// The UserAccounts module owns its persistence. This is the provider-agnostic context; providers
/// (<c>.PostgreSql</c>/<c>.Sqlite</c>) wire the connection and refine provider-specific storage.
/// <para>
/// On a successful save it collects the tracked aggregates' domain events and dispatches them <b>after the commit</b>
/// (ADR-017 §2.11 / Q8) through the module's <see cref="IDomainEventDispatcher"/>. The dispatcher is resolved by DI
/// (the WorkContext builds the context from the scoped service provider).
/// </para>
/// </summary>
public class UserAccountsDbContext : DbContext
{
	private readonly IDomainEventDispatcher dispatcher;

	/// <summary>
	/// Creates the context.
	/// </summary>
	/// <param name="options">The context options.</param>
	/// <param name="dispatcher">The domain event dispatcher (post-commit).</param>
	public UserAccountsDbContext(DbContextOptions<UserAccountsDbContext> options, IDomainEventDispatcher dispatcher)
		: base(options)
	{
		this.dispatcher = dispatcher;
	}

	/// <summary>
	/// Constructor for derived provider contexts.
	/// </summary>
	/// <param name="options">The context options.</param>
	/// <param name="dispatcher">The domain event dispatcher (post-commit).</param>
	protected UserAccountsDbContext(DbContextOptions options, IDomainEventDispatcher dispatcher)
		: base(options)
	{
		this.dispatcher = dispatcher;
	}

	/// <summary>
	/// Gets the account aggregate set.
	/// </summary>
	public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

	/// <summary>
	/// Gets the property scope aggregate set.
	/// </summary>
	public DbSet<PropertyScope> PropertyScopes => Set<PropertyScope>();

	/// <summary>
	/// Gets the account action token set (password recovery / email-phone verification / forced change).
	/// </summary>
	public DbSet<UserAccountActionToken> UserAccountActionTokens => Set<UserAccountActionToken>();

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyUserAccountsMappings();
	}

	/// <inheritdoc />
	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		var result = base.SaveChanges(acceptAllChangesOnSuccess);
		return ReconcileActiveVersionIds()
			? result + base.SaveChanges(acceptAllChangesOnSuccess)
			: result;
	}

	/// <inheritdoc />
	public override async Task<int> SaveChangesAsync(
		bool acceptAllChangesOnSuccess,
		CancellationToken cancellationToken = default)
	{
		// Collect (and clear) the aggregates' events before the commit, then dispatch them after it succeeds so an
		// observer never reacts to a change that did not persist (ADR-017 §2.11 / §10).
		var domainEvents = CollectAndClearDomainEvents();

		var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		if (ReconcileActiveVersionIds())
		{
			result += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		}

		await dispatcher.DispatchAsync(domainEvents, cancellationToken);

		return result;
	}

	/// <summary>
	/// Collects every tracked aggregate's domain events and clears the collections so a later save does not
	/// re-dispatch them. Returns the events in tracking order.
	/// </summary>
	private List<IDomainEvent> CollectAndClearDomainEvents()
	{
		var entries = ChangeTracker.Entries<IHasEvents>()
			.Where(e => e.Entity.DomainEvents is { Count: > 0 })
			.ToList();

		if (entries.Count is 0)
		{
			return [];
		}

		var domainEvents = new List<IDomainEvent>();
		foreach (var entry in entries)
		{
			domainEvents.AddRange(entry.Entity.DomainEvents!);
			entry.Entity.DomainEvents!.Clear();
		}

		return domainEvents;
	}

	/// <summary>
	/// Reconciles each tracked scope's denormalized <see cref="PropertyScope.ActiveVersionId"/> with the
	/// active version's now-generated key. Runs after the first save so version keys are available.
	/// </summary>
	private bool ReconcileActiveVersionIds()
	{
		var changed = false;

		foreach (var entry in ChangeTracker.Entries<PropertyScope>())
		{
			if (entry.State is EntityState.Deleted or EntityState.Detached)
			{
				continue;
			}

			if (entry.Entity.SyncActiveVersionId())
			{
				changed = true;
			}
		}

		return changed;
	}
}
