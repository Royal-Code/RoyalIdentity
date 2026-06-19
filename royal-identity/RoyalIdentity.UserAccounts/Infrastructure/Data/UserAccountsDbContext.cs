using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data;

/// <summary>
/// The UserAccounts module owns its persistence. This is the provider-agnostic context; providers
/// (<c>.PostgreSql</c>/<c>.Sqlite</c>) wire the connection and refine provider-specific storage.
/// </summary>
public class UserAccountsDbContext : DbContext
{
	/// <summary>
	/// Creates the context.
	/// </summary>
	/// <param name="options">The context options.</param>
	public UserAccountsDbContext(DbContextOptions<UserAccountsDbContext> options)
		: base(options)
	{
	}

	/// <summary>
	/// Constructor for derived provider contexts.
	/// </summary>
	/// <param name="options">The context options.</param>
	protected UserAccountsDbContext(DbContextOptions options)
		: base(options)
	{
	}

	/// <summary>
	/// Gets the account aggregate set.
	/// </summary>
	public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

	/// <summary>
	/// Gets the property scope aggregate set.
	/// </summary>
	public DbSet<PropertyScope> PropertyScopes => Set<PropertyScope>();

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserAccountsDbContext).Assembly);
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
		var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
		return ReconcileActiveVersionIds()
			? result + await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken)
			: result;
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
