using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 3 (plan-users-accounts-sqlite-hardening.md, Q8) — the single, test-only home for seeding the
/// UserAccounts module in tests. Physically lives in <c>Tests.UserAccounts</c> and is linked (not
/// project-referenced) into <c>Tests.Integration</c>, so both consumers compile the same source instead of
/// maintaining independent copies. There is deliberately no public seed API on the module itself (Q8): this
/// is test infrastructure, not a module capability.
/// <para>
/// Alice/Bob reuse the fake's subject ids (<see cref="MemoryStorage.AliceSubjectId"/>/
/// <see cref="MemoryStorage.BobSubjectId"/>) so a test written against the shared
/// <c>UserDirectoryContractTests</c> contract (or any HTTP characterization test) observes the same subject
/// identity regardless of which backing (fake or module) it runs against.
/// </para>
/// </summary>
public static class UserAccountsModuleSeed
{
	/// <summary>Alice's username, matching the in-memory fake's seeded account.</summary>
	public const string AliceUsername = "alice";

	/// <summary>Alice's password, matching the in-memory fake's seeded account.</summary>
	public const string AlicePassword = "alice";

	/// <summary>Bob's username, matching the in-memory fake's seeded account.</summary>
	public const string BobUsername = "bob";

	/// <summary>Bob's password, matching the in-memory fake's seeded account.</summary>
	public const string BobPassword = "bob";

	/// <summary>
	/// Seeds the "profile" and "email" property scopes used by the default claims projection, idempotently.
	/// </summary>
	public static async Task SeedDefaultScopesAsync(
		UserAccountsDbContext db, string realmId, DateTimeOffset now, CancellationToken ct = default)
	{
		await SeedScopeAsync(db, realmId, "profile", now, ct);
		await SeedScopeAsync(db, realmId, "email", now, ct);
	}

	/// <summary>Seeds a single active property scope by name, idempotently (no-op if it already exists).</summary>
	public static async Task SeedScopeAsync(
		UserAccountsDbContext db, string realmId, string scopeName, DateTimeOffset now, CancellationToken ct = default)
	{
		if (await db.PropertyScopes.AnyAsync(s => s.RealmId == realmId && s.Name == scopeName, ct))
		{
			return;
		}

		var propertyScope = new PropertyScope(realmId, scopeName, scopeName, now);
		var version = propertyScope.Versions.Single();
		var approveResult = propertyScope.ApproveVersion(version, now);
		if (approveResult.HasProblems(out var approveProblems))
		{
			throw new InvalidOperationException($"Could not seed UserAccounts property scope '{scopeName}': {approveProblems}");
		}

		db.PropertyScopes.Add(propertyScope);
		await db.SaveChangesAsync(ct);
	}

	/// <summary>
	/// Seeds the default Alice + Bob accounts (both admins, both active, both with the "profile"/"email" scopes
	/// already seeded) into the given realm, idempotently.
	/// </summary>
	public static async Task SeedDefaultAccountsAsync(
		IServiceProvider services,
		string realmId,
		UserAccountsRealmOptions options,
		DateTimeOffset now,
		CancellationToken ct = default)
	{
		await SeedAccountAsync(
			services, realmId, options,
			MemoryStorage.AliceSubjectId, AliceUsername, "Alice", "Alice@example.com", AlicePassword,
			isActive: true, roles: ["admin"], now, ct);
		await SeedAccountAsync(
			services, realmId, options,
			MemoryStorage.BobSubjectId, BobUsername, "Bob", "bob@example.com", BobPassword,
			isActive: true, roles: ["admin"], now, ct);
	}

	/// <summary>
	/// Seeds a single account via <see cref="ICreateUserAccountHandler"/>, idempotently (returns the existing
	/// account, unmodified, if one with the same realm/subject already exists).
	/// </summary>
	public static async Task<UserAccount> SeedAccountAsync(
		IServiceProvider services,
		string realmId,
		UserAccountsRealmOptions options,
		string subjectId,
		string username,
		string displayName,
		string email,
		string? password,
		bool isActive,
		IReadOnlyList<string> roles,
		DateTimeOffset now,
		CancellationToken ct = default)
	{
		var db = services.GetRequiredService<UserAccountsDbContext>();
		var existing = await db.UserAccounts
			.SingleOrDefaultAsync(a => a.RealmId == realmId && a.SubjectId == subjectId, ct);
		if (existing is not null)
		{
			return existing;
		}

		var handler = services.GetRequiredService<ICreateUserAccountHandler>();
		var result = await handler.HandleAsync(new CreateUserAccount
		{
			RealmId = realmId,
			Options = options,
			Username = username,
			DisplayName = displayName,
			Email = email,
			EmailVerified = true,
			Password = password,
			SubjectId = subjectId,
			Roles = roles
		}, ct);

		if (!result.HasValue(out var account))
		{
			throw new InvalidOperationException($"Could not seed UserAccounts user '{username}'.");
		}

		if (!isActive)
		{
			account.Deactivate(now);
			await db.SaveChangesAsync(ct);
		}

		return account;
	}
}
