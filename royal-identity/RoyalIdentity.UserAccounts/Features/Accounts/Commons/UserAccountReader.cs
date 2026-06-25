using Microsoft.EntityFrameworkCore;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Realm-scoped read access to account aggregates and active property scopes. Every query filters by
/// <c>RealmId</c> so data never crosses realms, and loads the graph the use cases need (credential, emails,
/// roles, property values) without relying on lazy loading.
/// </summary>
public sealed class UserAccountReader(UserAccountsDbContext db, IUserAccountNormalizer normalizer)
{
	/// <summary>
	/// Finds a tracked account by its immutable subject identifier within a realm.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="subjectId">The OIDC subject identifier.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The account with its graph loaded, or <c>null</c> when not found.</returns>
	public Task<UserAccount?> FindBySubjectIdAsync(string realmId, string subjectId, CancellationToken ct = default)
	{
		return AccountGraph()
			.FirstOrDefaultAsync(a => a.RealmId == realmId && a.SubjectId == subjectId, ct);
	}

	/// <summary>
	/// Finds a tracked account by its physical identifier within a realm. Used by flows that resolve the account
	/// from a stored foreign key (for example, after looking up an action token by its hash).
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="accountId">The physical account identifier.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The account with its graph loaded, or <c>null</c> when not found.</returns>
	public Task<UserAccount?> FindByIdAsync(string realmId, long accountId, CancellationToken ct = default)
	{
		return AccountGraph()
			.FirstOrDefaultAsync(a => a.RealmId == realmId && a.Id == accountId, ct);
	}

	/// <summary>
	/// Finds a tracked account by login, honoring the realm's username/email login policies.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="login">The raw login (username or email).</param>
	/// <param name="options">The realm account policies.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The account with its graph loaded, or <c>null</c> when not found.</returns>
	public async Task<UserAccount?> FindByLoginAsync(
		string realmId,
		string login,
		UserAccountsRealmOptions options,
		CancellationToken ct = default)
	{
		var normalizedUsername = normalizer.NormalizeUsername(login);
		var byUsername = await AccountGraph()
			.FirstOrDefaultAsync(a => a.RealmId == realmId && a.NormalizedUsername == normalizedUsername, ct);
		if (byUsername is not null)
		{
			return byUsername;
		}

		if (!options.LoginWithEmail && !options.EmailAsUsername)
		{
			return null;
		}

		var normalizedEmail = normalizer.NormalizeEmail(login);
		var emailQuery = db.Set<UserAccountEmail>()
			.Where(e => e.RealmId == realmId && e.NormalizedAddress == normalizedEmail);

		// An unverified email is only trusted for login when the realm does not enforce verification.
		if (options.VerifyEmail)
		{
			emailQuery = emailQuery.Where(e => e.IsVerified);
		}

		var match = await emailQuery
			.OrderByDescending(e => e.IsPrimary)
			.ThenBy(e => e.UserAccountId)
			.Select(e => new { e.UserAccountId })
			.FirstOrDefaultAsync(ct);
		if (match is null)
		{
			return null;
		}

		return await AccountGraph()
			.FirstOrDefaultAsync(a => a.RealmId == realmId && a.Id == match.UserAccountId, ct);
	}

	/// <summary>
	/// Loads the active property scopes for a realm restricted to the requested scope names, with each scope's
	/// active version and definition versions, for claim projection.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="scopeNames">The requested identity scope names.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The matching active property scopes.</returns>
	public async Task<IReadOnlyList<PropertyScope>> LoadActiveScopesAsync(
		string realmId,
		IEnumerable<string> scopeNames,
		CancellationToken ct = default)
	{
		var names = scopeNames.Distinct(StringComparer.Ordinal).ToList();
		if (names.Count is 0)
		{
			return [];
		}

		return await ScopeGraph()
			.Where(s => s.RealmId == realmId && s.IsActive && names.Contains(s.Name))
			.ToListAsync(ct);
	}

	/// <summary>
	/// Finds an active property scope by its identity scope name, with versions, definition versions and
	/// stable definitions loaded for write validation.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="scopeName">The identity scope name.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The active property scope, or <c>null</c> when not found.</returns>
	public Task<PropertyScope?> FindScopeByNameAsync(string realmId, string scopeName, CancellationToken ct = default)
	{
		return ScopeGraph()
			.FirstOrDefaultAsync(s => s.RealmId == realmId && s.Name == scopeName, ct);
	}

	private IQueryable<UserAccount> AccountGraph()
	{
		return db.UserAccounts
			.Include(a => a.LocalCredential)
			.Include("EmailItems")
			.Include("PhoneItems")
			.Include("RoleItems")
			.Include("PropertyValueItems")
			.Include("PasswordHistoryItems");
	}

	private IQueryable<PropertyScope> ScopeGraph()
	{
		return db.PropertyScopes
			.Include("VersionItems.DefinitionVersionItems.PropertyDefinition")
			.Include("DefinitionItems");
	}
}
