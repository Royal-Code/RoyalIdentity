using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 10 (plan-users-security-lifecycle.md) — the 7 concurrency scenarios of the pre-plan §10, proven against the
/// real module + SQLite. Credential/stamp mutations are guarded by the aggregate's <c>Version</c> concurrency token
/// (optimistic concurrency + retry, Q11); action tokens are single-use through an idempotent conditional update. The
/// in-memory SQLite connection is shared across scopes, so two contexts observe the same row and the interleaving is
/// real (the conflict is detected, not simulated).
/// </summary>
public class ConcurrencyTests
{
	private static readonly DateTimeOffset Start = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
	private static readonly FakeHasher Hasher = new();

	// Scenario 1 — two simultaneous password failures must not lose an increment.
	[Fact]
	public async Task Scenario1_TwoSimultaneousFailures_OptimisticConcurrency_PreventsLostIncrement()
	{
		var options = new PasswordOptions { MaxFailedAccessAttempts = 5, AccountLockoutDurationMinutes = 15 };
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var dbA = Context(scopeA);
		var dbB = Context(scopeB);
		var accountA = await LoadAsync(dbA, id);
		var accountB = await LoadAsync(dbB, id);

		// Both observe the same version and register a failure.
		accountA.AuthenticateLocal("wrong", options, Hasher, Start);
		accountB.AuthenticateLocal("wrong", options, Hasher, Start);

		await dbA.SaveChangesAsync();
		// The stale writer is rejected by the Version concurrency token: the increment is not lost, it must retry.
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

		// Retry path: reload on the current version and re-apply.
		using var scopeC = provider.CreateScope();
		var dbC = Context(scopeC);
		var accountC = await LoadAsync(dbC, id);
		accountC.AuthenticateLocal("wrong", options, Hasher, Start);
		await dbC.SaveChangesAsync();

		Assert.Equal(2, accountC.LocalCredential.FailedPasswordAttempts);
	}

	// Scenario 2 — a valid login concurrent with a failure must not leave the account blocked.
	[Fact]
	public async Task Scenario2_SuccessConcurrentWithFailure_OptimisticConcurrency_DetectsConflict()
	{
		var options = new PasswordOptions { MaxFailedAccessAttempts = 5, AccountLockoutDurationMinutes = 15 };
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var dbA = Context(scopeA);
		var dbB = Context(scopeB);
		var success = await LoadAsync(dbA, id);
		var failure = await LoadAsync(dbB, id);

		Assert.True(success.AuthenticateLocal("secret", options, Hasher, Start).Success);
		failure.AuthenticateLocal("wrong", options, Hasher, Start);

		await dbA.SaveChangesAsync();
		// The stale failure observed the pre-login version and cannot persist over the success.
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

		using var scopeC = provider.CreateScope();
		var after = await LoadAsync(Context(scopeC), id);
		Assert.Equal(0, after.LocalCredential.FailedPasswordAttempts);
		Assert.Null(after.LocalCredential.LockoutEndAt);
	}

	// Scenario 3 — the same token consumed twice must succeed at most once.
	[Fact]
	public async Task Scenario3_DoubleTokenConsumption_ConditionalUpdate_AllowsExactlyOnce()
	{
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, new PasswordOptions());

		long tokenId;
		using (var scope = provider.CreateScope())
		{
			var db = Context(scope);
			var account = await LoadAsync(db, id);
			var tokens = new UserAccountActionTokenService(db);
			var raw = await tokens.IssueAsync(account, ActionTokenPurpose.PasswordRecovery, null, Start, Start.AddHours(1));
			await db.SaveChangesAsync();
			var candidate = await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, raw, Start);
			tokenId = candidate!.TokenId;
		}

		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var tokensA = new UserAccountActionTokenService(Context(scopeA));
		var tokensB = new UserAccountActionTokenService(Context(scopeB));

		var firstWon = await tokensA.TryConsumeAsync(tokenId, Start);
		var secondWon = await tokensB.TryConsumeAsync(tokenId, Start);

		Assert.True(firstWon);
		Assert.False(secondWon);
	}

	// Scenario 4 — re-issuing a token revokes the previous one; the old token stops working.
	[Fact]
	public async Task Scenario4_ReissueRevokesPreviousToken_OldTokenNoLongerConsumable()
	{
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, new PasswordOptions());

		using var scope = provider.CreateScope();
		var db = Context(scope);
		var account = await LoadAsync(db, id);
		var tokens = new UserAccountActionTokenService(db);

		var rawOld = await tokens.IssueAsync(account, ActionTokenPurpose.PasswordRecovery, null, Start, Start.AddHours(1));
		await db.SaveChangesAsync();
		var oldCandidate = await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, rawOld, Start);
		Assert.NotNull(oldCandidate);

		// A new issuance revokes the account's previous active token of the same purpose.
		var rawNew = await tokens.IssueAsync(
			account, ActionTokenPurpose.PasswordRecovery, null, Start.AddMinutes(1), Start.AddHours(1));
		await db.SaveChangesAsync();

		var probe = Start.AddMinutes(2);
		Assert.Null(await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, rawOld, probe));
		Assert.NotNull(await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, rawNew, probe));
		// Even consuming the old token by id (captured before the re-issue) affects zero rows.
		Assert.False(await tokens.TryConsumeAsync(oldCandidate!.TokenId, probe));
	}

	// Scenario 5 — a password reset concurrent with a stale login conflicts and moves both security markers.
	[Fact]
	public async Task Scenario5_PasswordResetConcurrentWithStaleAuth_DetectsConflict_AndMovesSecurityMarkers()
	{
		var options = new PasswordOptions { MaxFailedAccessAttempts = 5 };
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var dbA = Context(scopeA);
		var dbB = Context(scopeB);
		var changer = await LoadAsync(dbA, id);
		var stale = await LoadAsync(dbB, id);

		var stampBefore = changer.SecurityStamp.Value;
		var validAfterBefore = changer.SessionsValidAfter;

		// A recovery reset is a credential-compromise trigger: it moves SecurityStamp and SessionsValidAfter.
		changer.ResetPassword("hashed:renewed", options, Start.AddMinutes(5));
		await dbA.SaveChangesAsync();

		// A login that observed the pre-reset version cannot persist a session-bearing state over it.
		stale.AuthenticateLocal("secret", options, Hasher, Start.AddMinutes(5));
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

		using var scopeC = provider.CreateScope();
		var after = await LoadAsync(Context(scopeC), id);
		Assert.NotEqual(stampBefore, after.SecurityStamp.Value);
		Assert.True(after.SessionsValidAfter > validAfterBefore);
	}

	// Scenario 6 — an admin unlock concurrent with a stale failure must not be re-locked by the failure.
	[Fact]
	public async Task Scenario6_AdminUnlockConcurrentWithStaleFailure_DetectsConflict()
	{
		var options = new PasswordOptions { MaxFailedAccessAttempts = 3, AccountLockoutDurationMinutes = 0 };
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		// Drive to one failure short of lockout, so both readers load an unlocked (mutable) version.
		using (var seedFailures = provider.CreateScope())
		{
			var db = Context(seedFailures);
			var account = await LoadAsync(db, id);
			for (var i = 0; i < options.MaxFailedAccessAttempts - 1; i++)
			{
				account.AuthenticateLocal($"wrong-{i}", options, Hasher, Start);
			}

			await db.SaveChangesAsync();
			Assert.Equal(options.MaxFailedAccessAttempts - 1, account.LocalCredential.FailedPasswordAttempts);
		}

		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var dbA = Context(scopeA);
		var dbB = Context(scopeB);
		var admin = await LoadAsync(dbA, id);
		var stale = await LoadAsync(dbB, id);

		// Admin unlock clears counters and bumps the version.
		admin.UnlockLocalCredential(Start.AddMinutes(1));
		await dbA.SaveChangesAsync();

		// The stale failure (which would reach the lockout threshold) observed the pre-unlock version.
		stale.AuthenticateLocal("wrong-final", options, Hasher, Start.AddMinutes(1));
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());

		using var scopeC = provider.CreateScope();
		var after = await LoadAsync(Context(scopeC), id);
		Assert.Equal(0, after.LocalCredential.FailedPasswordAttempts);
		Assert.False(after.LocalCredential.IsLockedOut(options, Start.AddMinutes(1)));
	}

	// Scenario 7 — a verification token bound to a value verifies only that value, never a later primary.
	[Fact]
	public async Task Scenario7_EmailVerificationTargetsValue_StaleTokenDoesNotVerifyNewPrimary()
	{
		await using var provider = BuildProvider();
		var options = new PasswordOptions();
		long id;

		using (var scope = provider.CreateScope())
		{
			var db = Context(scope);
			var account = new UserAccount("r1", "alice", "alice", "ALICE", "Alice", Start);
			account.SetPassword("hashed:secret", Start, options, PasswordChangeReason.Create);
			Assert.True(account.AddEmail(
				new UserAccountEmail("r1", "old@example.com", "OLD@EXAMPLE.COM", true, false, false), Start).IsSuccess);
			db.UserAccounts.Add(account);
			await db.SaveChangesAsync();
			id = account.Id;
		}

		string targetValue;
		long tokenId;
		using (var scope = provider.CreateScope())
		{
			var db = Context(scope);
			var account = await db.UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);
			var tokens = new UserAccountActionTokenService(db);
			var raw = await tokens.IssueAsync(
				account, ActionTokenPurpose.EmailVerification, "OLD@EXAMPLE.COM", Start, Start.AddHours(1));
			await db.SaveChangesAsync();
			var candidate = await tokens.FindConsumableAsync("r1", ActionTokenPurpose.EmailVerification, raw, Start);
			targetValue = candidate!.TargetValue!;
			tokenId = candidate.TokenId;
		}

		// The user changes the primary email before consuming the token: a new (unverified) primary is added.
		using (var scope = provider.CreateScope())
		{
			var db = Context(scope);
			var account = await db.UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);
			Assert.True(account.AddEmail(
				new UserAccountEmail("r1", "new@example.com", "NEW@EXAMPLE.COM", true, false, false),
				Start.AddMinutes(1)).IsSuccess);
			await db.SaveChangesAsync();
		}

		using (var scope = provider.CreateScope())
		{
			var db = Context(scope);
			var account = await db.UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);
			var tokens = new UserAccountActionTokenService(db);

			Assert.True(await tokens.TryConsumeAsync(tokenId, Start.AddMinutes(2)));
			// The use case verifies the token's bound value, not the current primary.
			Assert.True(account.VerifyEmail(targetValue, Start.AddMinutes(2)).IsSuccess);
			await db.SaveChangesAsync();
		}

		using (var scope = provider.CreateScope())
		{
			var db = Context(scope);
			var account = await db.UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);

			var old = account.Emails.Single(e => e.NormalizedAddress == "OLD@EXAMPLE.COM");
			var current = account.Emails.Single(e => e.NormalizedAddress == "NEW@EXAMPLE.COM");

			// Only the bound (old) value is verified; the new primary stays unverified (email_verified would be false).
			Assert.True(old.IsVerified);
			Assert.False(old.IsPrimary);
			Assert.True(current.IsPrimary);
			Assert.False(current.IsVerified);
		}
	}

	// ---- harness ----

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeClock(Start));
		services.AddSingleton<IUserAccountPasswordHasher>(Hasher);
		services.AddUserAccountsSqliteInMemory();
		return services.BuildServiceProvider();
	}

	private static UserAccountsSqliteDbContext Context(IServiceScope scope)
		=> scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();

	private static Task<UserAccount> LoadAsync(UserAccountsSqliteDbContext db, long id)
		=> db.UserAccounts.Include(a => a.LocalCredential).FirstAsync(a => a.Id == id);

	private static async Task<long> SeedAccountAsync(
		ServiceProvider provider, PasswordOptions options, string subjectId = "alice")
	{
		using var scope = provider.CreateScope();
		var db = Context(scope);
		var account = new UserAccount("r1", subjectId, subjectId, subjectId.ToUpperInvariant(), "Alice", Start);
		account.SetPassword("hashed:secret", Start, options, PasswordChangeReason.Create);
		db.UserAccounts.Add(account);
		await db.SaveChangesAsync();
		return account.Id;
	}

	private sealed class FakeClock(DateTimeOffset start) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => start;
	}

	private sealed class FakeHasher : IUserAccountPasswordHasher
	{
		public string Hash(string password) => $"hashed:{password}";

		public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
	}
}
