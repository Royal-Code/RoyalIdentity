using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;
using RoyalCode.UnitOfWork;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 1 (plan-users-accounts-sqlite-hardening.md) — proves the real command handlers are resilient to
/// optimistic-concurrency conflicts (ADR-017 §2.9), not just able to detect them.
/// <para>
/// The retry scenarios below pre-track a stale account instance in one DI scope's <c>DbContext</c> (via
/// <see cref="UserAccountReader"/>), then bump the real row's <c>Version</c> from a separate scope/DbContext
/// sharing the same in-memory SQLite connection, and finally resolve the handler <em>from the stale scope</em>:
/// EF's identity map hands the handler's own internal reload the already-tracked (stale) instance, so its first
/// save genuinely conflicts with the row modified meanwhile — the conflict is real, not simulated.
/// </para>
/// <para>
/// The token-consumption scenarios (idempotency, value-bound verification) do not involve the retry loop and are
/// unaffected by Fase 1; they remain as direct, low-level EF assertions.
/// </para>
/// </summary>
public class ConcurrencyTests
{
	private static readonly DateTimeOffset Start = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
	private static readonly FakeHasher Hasher = new();

	// ---- retry-eligible use cases: the real handler recovers from a genuine conflict ----

	[Fact]
	public async Task UnlockPasswordCredential_RetriesOnConflict_AndSucceeds()
	{
		// [WithRetryOnConcurrency] wraps the whole handler body: a conflict on save rolls back, clears the
		// tracker and re-runs { find -> mutate -> save } with fresh state.
		var options = new PasswordOptions { MaxFailedAccessAttempts = 3, AccountLockoutDurationMinutes = 0 };
		await using var provider = BuildProvider();
		await SeedAccountAsync(provider, options);

		// Drive to one failure short of lockout before either reader observes the account.
		using (var seeding = provider.CreateScope())
		{
			var account = await Reader(seeding).FindBySubjectIdAsync("r1", "alice");
			account!.AuthenticateLocal("wrong-1", options, Hasher, Start);
			await Db(seeding).SaveChangesAsync();
		}

		using var scopeStale = provider.CreateScope();
		// Tracks the account at 1 failure / unlocked — this instance goes stale the moment the writer below saves.
		await Reader(scopeStale).FindBySubjectIdAsync("r1", "alice");

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindBySubjectIdAsync("r1", "alice");
			// The 2nd (final) failure locks the account and bumps the version the stale reader does not see.
			account!.AuthenticateLocal("wrong-2", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var handler = scopeStale.ServiceProvider.GetRequiredService<IUnlockPasswordCredentialHandler>();
		var result = await handler.HandleAsync(new UnlockPasswordCredential { RealmId = "r1", SubjectId = "alice" }, default);

		// The conflict is transparent to the caller: the first attempt fails on the stale (unlocked) snapshot,
		// the retry reloads the real (locked) row and clears it for good.
		Assert.True(result.IsSuccess);

		using var scopeAssert = provider.CreateScope();
		var after = await Reader(scopeAssert).FindBySubjectIdAsync("r1", "alice");
		Assert.Equal(0, after!.LocalCredential.FailedPasswordAttempts);
		Assert.False(after.LocalCredential.IsLockedOut(options, Start));
	}

	[Fact]
	public async Task ResetPasswordWithToken_RetriesAggregateMutationOnly_TokenConsumedExactlyOnce()
	{
		// ResetPasswordWithToken cannot use [WithRetryOnConcurrency] (it would re-run the single-use token
		// consumption on retry); it scopes IWorkContext.RetryOnConcurrencyAsync to the reload+reapply+save only.
		var options = new PasswordOptions();
		var realmOptions = UserAccountsTestOptions.Relaxed();
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		string raw;
		using (var seeding = provider.CreateScope())
		{
			var account = await Reader(seeding).FindBySubjectIdAsync("r1", "alice");
			var tokens = seeding.ServiceProvider.GetRequiredService<UserAccountActionTokenService>();
			raw = await tokens.IssueAsync(account!, ActionTokenPurpose.PasswordRecovery, null, Start, Start.AddHours(1));
			await Db(seeding).SaveChangesAsync();
		}

		using var scopeStale = provider.CreateScope();
		// Tracks the account before the concurrent write below; the reset handler resolved from this same
		// scope will reuse this stale instance on its first attempt.
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.AuthenticateLocal("wrong", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var handler = scopeStale.ServiceProvider.GetRequiredService<IResetPasswordWithTokenHandler>();
		var result = await handler.HandleAsync(new ResetPasswordWithToken
		{
			RealmId = "r1",
			Options = realmOptions,
			Token = raw,
			NewPassword = "renewed-secret"
		}, default);

		Assert.True(result.IsSuccess);

		using var scopeAssert = provider.CreateScope();
		var after = await Reader(scopeAssert).FindByIdAsync("r1", id);
		Assert.True(after!.VerifyCurrentPassword("renewed-secret", Hasher).IsSuccess);

		// Single-use: the same token cannot reset the password again, regardless of the earlier retry.
		using var scopeReplay = provider.CreateScope();
		var replayHandler = scopeReplay.ServiceProvider.GetRequiredService<IResetPasswordWithTokenHandler>();
		var replay = await replayHandler.HandleAsync(new ResetPasswordWithToken
		{
			RealmId = "r1",
			Options = realmOptions,
			Token = raw,
			NewPassword = "another-secret"
		}, default);
		Assert.True(replay.HasProblems(out _));
	}

	[Fact]
	public async Task ChangeOwnPassword_WhenRetryBudgetIsExhausted_ReturnsConcurrencyConflictProblem()
	{
		// MaxAttempts = 1 means a single attempt, no retry: the first (and only) conflict exhausts immediately.
		var options = new PasswordOptions();
		var realmOptions = UserAccountsTestOptions.Relaxed();
		await using var provider = BuildProvider(maxAttempts: 1);
		var id = await SeedAccountAsync(provider, options);

		using var scopeStale = provider.CreateScope();
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.AuthenticateLocal("wrong", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var handler = scopeStale.ServiceProvider.GetRequiredService<IChangeOwnPasswordHandler>();
		var result = await handler.HandleAsync(new ChangeOwnPassword
		{
			RealmId = "r1",
			Options = realmOptions,
			SubjectId = "alice",
			CurrentPassword = "secret",
			NewPassword = "brand-new-secret"
		}, default);

		Assert.True(result.HasProblems(out var problems));
		var problem = Assert.Single(problems);
		Assert.Equal(ProblemCategory.InvalidState, problem.Category);
		Assert.Equal("user_account.concurrency_conflict", problem.TypeId);
	}

	[Fact]
	public async Task AuthenticateLocalCredential_HasNoRetry_ConflictReturnsControlledProblem_NotAnUnhandledException()
	{
		// Q4 (best-effort): authentication is deliberately excluded from retry. A conflict on save must not be
		// silently retried, but it also must not leak a raw ConcurrencyException to the edge — the module catches
		// it and returns a controlled Problem (fail-closed: the stale-computed outcome is discarded).
		var options = new PasswordOptions { MaxFailedAccessAttempts = 5, AccountLockoutDurationMinutes = 15 };
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		using var scopeStale = provider.CreateScope();
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.AuthenticateLocal("wrong-elsewhere", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var handler = scopeStale.ServiceProvider.GetRequiredService<IAuthenticateLocalCredentialHandler>();

		var result = await handler.HandleAsync(new AuthenticateLocalCredential
		{
			RealmId = "r1",
			Options = UserAccountsTestOptions.Relaxed(),
			Login = "alice",
			Password = "wrong-stale"
		}, default);

		Assert.True(result.HasProblems(out var problems));
		var problem = Assert.Single(problems);
		Assert.Equal(ProblemCategory.InvalidState, problem.Category);
		Assert.Equal("user_account.concurrency_conflict", problem.TypeId);
		Assert.Empty(Db(scopeStale).ChangeTracker.Entries());

		// The failed attempt observed on the stale snapshot was not persisted (fail-closed): the real (concurrent)
		// state from the writer above is what remains — 1 failed attempt, not 2.
		using var scopeAssert = provider.CreateScope();
		var after = await Reader(scopeAssert).FindByIdAsync("r1", id);
		Assert.Equal(1, after!.LocalCredential.FailedPasswordAttempts);
	}

	[Fact]
	public async Task ChangeExpiredPasswordWithToken_RetriesAggregateMutationOnly_TokenConsumedExactlyOnce()
	{
		var options = new PasswordOptions();
		var realmOptions = UserAccountsTestOptions.Relaxed();
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options);

		string raw;
		using (var seeding = provider.CreateScope())
		{
			var account = await Reader(seeding).FindByIdAsync("r1", id);
			account!.SetPassword(Hasher.Hash("secret"), Start, options, PasswordChangeReason.AdminSet, mustChangePassword: true);
			var tokens = seeding.ServiceProvider.GetRequiredService<UserAccountActionTokenService>();
			raw = await tokens.IssueAsync(account, ActionTokenPurpose.ChangeExpiredPassword, null, Start, Start.AddHours(1));
			await Db(seeding).SaveChangesAsync();
		}

		using var scopeStale = provider.CreateScope();
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.AuthenticateLocal("wrong", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var handler = scopeStale.ServiceProvider.GetRequiredService<IChangeExpiredPasswordWithTokenHandler>();
		var result = await handler.HandleAsync(new ChangeExpiredPasswordWithToken
		{
			RealmId = "r1",
			Options = realmOptions,
			Token = raw,
			NewPassword = "renewed-secret"
		}, default);

		Assert.True(result.IsSuccess);

		using var scopeAssert = provider.CreateScope();
		var after = await Reader(scopeAssert).FindByIdAsync("r1", id);
		Assert.False(after!.LocalCredential.MustChangePassword);
		Assert.True(after.VerifyCurrentPassword("renewed-secret", Hasher).IsSuccess);

		using var scopeReplay = provider.CreateScope();
		var replayHandler = scopeReplay.ServiceProvider.GetRequiredService<IChangeExpiredPasswordWithTokenHandler>();
		var replay = await replayHandler.HandleAsync(new ChangeExpiredPasswordWithToken
		{
			RealmId = "r1",
			Options = realmOptions,
			Token = raw,
			NewPassword = "another-secret"
		}, default);
		Assert.True(replay.HasProblems(out _));
	}

	[Fact]
	public async Task ConfirmEmailVerification_RetriesAggregateMutationOnly_TokenConsumedExactlyOnce()
	{
		var options = new PasswordOptions();
		await using var provider = BuildProvider();
		long id;
		string raw;

		using (var seeding = provider.CreateScope())
		{
			var db = Db(seeding);
			var account = new UserAccount("r1", "alice", "alice", "ALICE", "Alice", Start);
			account.SetPassword(Hasher.Hash("secret"), Start, options, PasswordChangeReason.Create);
			Assert.True(account.AddEmail(
				new UserAccountEmail("r1", "alice@example.com", "ALICE@EXAMPLE.COM", true, false, false), Start).IsSuccess);
			db.UserAccounts.Add(account);
			await db.SaveChangesAsync();
			id = account.Id;

			var tokens = seeding.ServiceProvider.GetRequiredService<UserAccountActionTokenService>();
			raw = await tokens.IssueAsync(account, ActionTokenPurpose.EmailVerification, "ALICE@EXAMPLE.COM", Start, Start.AddHours(1));
			await db.SaveChangesAsync();
		}

		using var scopeStale = provider.CreateScope();
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.AuthenticateLocal("wrong", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var realmOptions = UserAccountsTestOptions.Relaxed();
		var handler = scopeStale.ServiceProvider.GetRequiredService<IConfirmEmailVerificationHandler>();
		var result = await handler.HandleAsync(
			new ConfirmEmailVerification { RealmId = "r1", Options = realmOptions, Token = raw }, default);

		Assert.True(result.IsSuccess);

		using var scopeAssert = provider.CreateScope();
		var after = await Db(scopeAssert).UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);
		Assert.True(after.Emails.Single().IsVerified);

		using var scopeReplay = provider.CreateScope();
		var replayHandler = scopeReplay.ServiceProvider.GetRequiredService<IConfirmEmailVerificationHandler>();
		var replay = await replayHandler.HandleAsync(
			new ConfirmEmailVerification { RealmId = "r1", Options = realmOptions, Token = raw }, default);
		Assert.True(replay.HasProblems(out _));
	}

	[Fact]
	public async Task ConfirmPhoneVerification_RetriesAggregateMutationOnly_TokenConsumedExactlyOnce()
	{
		var options = new PasswordOptions();
		await using var provider = BuildProvider();
		long id;
		string raw;

		using (var seeding = provider.CreateScope())
		{
			var db = Db(seeding);
			var account = new UserAccount("r1", "alice", "alice", "ALICE", "Alice", Start);
			account.SetPassword(Hasher.Hash("secret"), Start, options, PasswordChangeReason.Create);
			Assert.True(account.AddPhone(
				new UserAccountPhone("r1", "+15550100", "+15550100", true, false), Start).IsSuccess);
			db.UserAccounts.Add(account);
			await db.SaveChangesAsync();
			id = account.Id;

			var tokens = seeding.ServiceProvider.GetRequiredService<UserAccountActionTokenService>();
			raw = await tokens.IssueAsync(account, ActionTokenPurpose.PhoneVerification, "+15550100", Start, Start.AddHours(1));
			await db.SaveChangesAsync();
		}

		using var scopeStale = provider.CreateScope();
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.AuthenticateLocal("wrong", options, Hasher, Start);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var realmOptions = UserAccountsTestOptions.Relaxed();
		realmOptions.EnablePhoneNumber = true;
		var handler = scopeStale.ServiceProvider.GetRequiredService<IConfirmPhoneVerificationHandler>();
		var result = await handler.HandleAsync(
			new ConfirmPhoneVerification { RealmId = "r1", Options = realmOptions, Token = raw }, default);

		Assert.True(result.IsSuccess);

		using var scopeAssert = provider.CreateScope();
		var after = await Db(scopeAssert).UserAccounts.Include("PhoneItems").FirstAsync(a => a.Id == id);
		Assert.True(after.Phones.Single().IsVerified);

		using var scopeReplay = provider.CreateScope();
		var replayHandler = scopeReplay.ServiceProvider.GetRequiredService<IConfirmPhoneVerificationHandler>();
		var replay = await replayHandler.HandleAsync(
			new ConfirmPhoneVerification { RealmId = "r1", Options = realmOptions, Token = raw }, default);
		Assert.True(replay.HasProblems(out _));
	}

	[Fact]
	public async Task ResetPasswordWithToken_RevalidatesHistoryOnReloadedState_RejectsCandidateThatBecameReuseMeanwhile()
	{
		// Achado 2 (review): the pre-consumption history check ran against the version that existed when this
		// request started. If a concurrent write makes the very candidate the user is submitting collide with the
		// (now-current) password by the time the retry's reloaded state is evaluated, the aggregate itself has no
		// defense (SetPassword/ResetPassword never re-check history) — the retry body must re-validate history
		// against the fresh reload, not just blindly reapply.
		var options = new PasswordOptions { EnforcePasswordHistory = true };
		var realmOptions = UserAccountsTestOptions.Relaxed();
		realmOptions.PasswordOptions.EnforcePasswordHistory = true;
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, options); // current password: "secret"

		string raw;
		using (var seeding = provider.CreateScope())
		{
			var account = await Reader(seeding).FindByIdAsync("r1", id);
			var tokens = seeding.ServiceProvider.GetRequiredService<UserAccountActionTokenService>();
			raw = await tokens.IssueAsync(account!, ActionTokenPurpose.PasswordRecovery, null, Start, Start.AddHours(1));
			await Db(seeding).SaveChangesAsync();
		}

		using var scopeStale = provider.CreateScope();
		// Tracked while the current password is still "secret" — the user's submitted "concurrent-secret" does
		// not collide with anything yet from this scope's point of view.
		await Reader(scopeStale).FindByIdAsync("r1", id);

		using (var scopeWriter = provider.CreateScope())
		{
			// Someone else changes the password to exactly the value our stale requester is about to submit,
			// concurrently, and bumps the version the stale reader does not see.
			var account = await Reader(scopeWriter).FindByIdAsync("r1", id);
			account!.SetPassword(Hasher.Hash("concurrent-secret"), Start, options, PasswordChangeReason.Change);
			await Db(scopeWriter).SaveChangesAsync();
		}

		var handler = scopeStale.ServiceProvider.GetRequiredService<IResetPasswordWithTokenHandler>();
		var result = await handler.HandleAsync(new ResetPasswordWithToken
		{
			RealmId = "r1",
			Options = realmOptions,
			Token = raw,
			NewPassword = "concurrent-secret"
		}, default);

		// Must reject: by the time the retry re-validates against the fresh reload, "concurrent-secret" is the
		// current password — reusing it would violate history/reuse policy despite passing the pre-consumption
		// check against the stale snapshot.
		Assert.True(result.HasProblems(out _));

		using var scopeAssertFinal = provider.CreateScope();
		var final = await Reader(scopeAssertFinal).FindByIdAsync("r1", id);
		Assert.True(final!.VerifyCurrentPassword("concurrent-secret", Hasher).IsSuccess);
	}

	// ---- token consumption: idempotency and value-bound verification (unrelated to the retry loop) ----

	// A verification token bound to a value verifies only that value, never a later primary.
	[Fact]
	public async Task EmailVerificationTargetsValue_StaleTokenDoesNotVerifyNewPrimary()
	{
		await using var provider = BuildProvider();
		var options = new PasswordOptions();
		long id;

		using (var scope = provider.CreateScope())
		{
			var db = Db(scope);
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
			var db = Db(scope);
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
			var db = Db(scope);
			var account = await db.UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);
			Assert.True(account.AddEmail(
				new UserAccountEmail("r1", "new@example.com", "NEW@EXAMPLE.COM", true, false, false),
				Start.AddMinutes(1)).IsSuccess);
			await db.SaveChangesAsync();
		}

		using (var scope = provider.CreateScope())
		{
			var db = Db(scope);
			var account = await db.UserAccounts.Include("EmailItems").FirstAsync(a => a.Id == id);
			var tokens = new UserAccountActionTokenService(db);

			Assert.True(await tokens.TryConsumeAsync(tokenId, Start.AddMinutes(2)));
			// The use case verifies the token's bound value, not the current primary.
			Assert.True(account.VerifyEmail(targetValue, Start.AddMinutes(2)).IsSuccess);
			await db.SaveChangesAsync();
		}

		using (var scope = provider.CreateScope())
		{
			var db = Db(scope);
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

	// The same token consumed twice must succeed at most once (conditional update, no retry involved).
	[Fact]
	public async Task DoubleTokenConsumption_ConditionalUpdate_AllowsExactlyOnce()
	{
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, new PasswordOptions());

		long tokenId;
		using (var scope = provider.CreateScope())
		{
			var db = Db(scope);
			var account = await Reader(scope).FindByIdAsync("r1", id);
			var tokens = new UserAccountActionTokenService(db);
			var raw = await tokens.IssueAsync(account!, ActionTokenPurpose.PasswordRecovery, null, Start, Start.AddHours(1));
			await db.SaveChangesAsync();
			var candidate = await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, raw, Start);
			tokenId = candidate!.TokenId;
		}

		using var scopeA = provider.CreateScope();
		using var scopeB = provider.CreateScope();
		var tokensA = new UserAccountActionTokenService(Db(scopeA));
		var tokensB = new UserAccountActionTokenService(Db(scopeB));

		var firstWon = await tokensA.TryConsumeAsync(tokenId, Start);
		var secondWon = await tokensB.TryConsumeAsync(tokenId, Start);

		Assert.True(firstWon);
		Assert.False(secondWon);
	}

	// Re-issuing a token revokes the previous one; the old token stops working.
	[Fact]
	public async Task ReissueRevokesPreviousToken_OldTokenNoLongerConsumable()
	{
		await using var provider = BuildProvider();
		var id = await SeedAccountAsync(provider, new PasswordOptions());

		using var scope = provider.CreateScope();
		var db = Db(scope);
		var account = await Reader(scope).FindByIdAsync("r1", id);
		var tokens = new UserAccountActionTokenService(db);

		var rawOld = await tokens.IssueAsync(account!, ActionTokenPurpose.PasswordRecovery, null, Start, Start.AddHours(1));
		await db.SaveChangesAsync();
		var oldCandidate = await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, rawOld, Start);
		Assert.NotNull(oldCandidate);

		// A new issuance revokes the account's previous active token of the same purpose.
		var rawNew = await tokens.IssueAsync(
			account!, ActionTokenPurpose.PasswordRecovery, null, Start.AddMinutes(1), Start.AddHours(1));
		await db.SaveChangesAsync();

		var probe = Start.AddMinutes(2);
		Assert.Null(await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, rawOld, probe));
		Assert.NotNull(await tokens.FindConsumableAsync("r1", ActionTokenPurpose.PasswordRecovery, rawNew, probe));
		// Even consuming the old token by id (captured before the re-issue) affects zero rows.
		Assert.False(await tokens.TryConsumeAsync(oldCandidate!.TokenId, probe));
	}

	// ---- harness ----

	private static ServiceProvider BuildProvider(int? maxAttempts = null)
	{
		var services = new ServiceCollection();
		services.AddSingleton<TimeProvider>(new FakeClock(Start));
		services.AddSingleton<IUserAccountPasswordHasher>(Hasher);
		var builder = services.AddUserAccountsSqliteInMemory();
		if (maxAttempts is not null)
		{
			// Overrides the module default (services.Configure runs in registration order; this one wins).
			builder.Services.Configure<RetryOnConcurrencyOptions>(o => o.MaxAttempts = maxAttempts.Value);
		}
		return services.BuildServiceProvider();
	}

	private static UserAccountsSqliteDbContext Db(IServiceScope scope)
		=> scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();

	private static UserAccountReader Reader(IServiceScope scope)
		=> scope.ServiceProvider.GetRequiredService<UserAccountReader>();

	private static async Task<long> SeedAccountAsync(
		ServiceProvider provider, PasswordOptions options, string subjectId = "alice")
	{
		using var scope = provider.CreateScope();
		var db = Db(scope);
		var account = new UserAccount("r1", subjectId, subjectId, subjectId.ToUpperInvariant(), "Alice", Start);
		account.SetPassword(Hasher.Hash("secret"), Start, options, PasswordChangeReason.Create);
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
