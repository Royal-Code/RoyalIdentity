using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.WorkContext;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.UserAccounts;

public class UserAccountsPersistenceTests
{
	private static readonly DateTimeOffset Now = new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task UserAccount_RoundTrips_WithCredentialEmailsRolesAndBlockState()
	{
		await using var provider = BuildProvider();
		long accountId;

		await using (var write = NewContext(provider))
		{
			var account = NewAccount();
			Assert.True(account.AddEmail(
				new UserAccountEmail("realm-a", "alice@example.com", "ALICE@EXAMPLE.COM", true, true, false), Now).IsSuccess);
			Assert.True(account.AddEmail(
				new UserAccountEmail("realm-a", "alice.alt@example.com", "ALICE.ALT@EXAMPLE.COM", false, false, false), Now).IsSuccess);
			Assert.True(account.AddRole(new UserAccountRole("realm-a", "admin", "ADMIN"), Now).IsSuccess);
			account.SetPassword("hashed-secret", Now, new PasswordOptions());
			account.Block("fraud", Now.AddMinutes(1));

			write.UserAccounts.Add(account);
			await write.SaveChangesAsync();
			accountId = account.Id;
		}

		Assert.True(accountId > 0);

		await using var read = NewContext(provider);
		var loaded = await read.UserAccounts
			.Include(a => a.LocalCredential)
			.Include("EmailItems")
			.Include("RoleItems")
			.SingleAsync(a => a.Id == accountId);

		Assert.Equal("realm-a", loaded.RealmId);
		Assert.Equal("subject-1", loaded.SubjectId);
		Assert.Equal("ALICE", loaded.NormalizedUsername);
		Assert.True(loaded.IsActive);
		Assert.True(loaded.IsBlocked);
		Assert.Equal("fraud", loaded.BlockedReason);
		Assert.Equal(Now.AddMinutes(1), loaded.BlockedAt);
		Assert.NotEmpty(loaded.SecurityStamp.Value);
		Assert.Equal(Now, loaded.SessionsValidAfter);
		Assert.Equal("hashed-secret", loaded.LocalCredential.PasswordHash);
		Assert.Equal(2, loaded.Emails.Count);
		Assert.Equal("alice@example.com", loaded.PrimaryEmail?.Address);
		Assert.Single(loaded.Roles, r => r.NormalizedName == "ADMIN");
	}

	[Fact]
	public async Task PasswordHistory_RoundTrips_AndIsPrunedToRetainedQuantity()
	{
		await using var provider = BuildProvider();
		var options = new PasswordOptions
		{
			EnforcePasswordHistory = true,
			PasswordHistoryCount = 2,
			PasswordReuseWindowDays = 0,
			MaxPasswordHistoryComparisons = 24
		};
		long accountId;

		await using (var write = NewContext(provider))
		{
			var account = NewAccount();
			for (var i = 1; i <= 5; i++)
			{
				Assert.True(account.SetPassword($"hashed:p{i}", Now.AddDays(i), options).IsSuccess);
			}

			write.UserAccounts.Add(account);
			await write.SaveChangesAsync();
			accountId = account.Id;
		}

		await using var read = NewContext(provider);
		var loaded = await read.UserAccounts
			.Include(a => a.LocalCredential)
			.Include("PasswordHistoryItems")
			.SingleAsync(a => a.Id == accountId);

		Assert.Equal("hashed:p5", loaded.LocalCredential.PasswordHash);
		var hashes = loaded.PasswordHistory.Select(h => h.PasswordHash).ToHashSet();
		Assert.Equal(2, hashes.Count);
		Assert.Contains("hashed:p3", hashes);
		Assert.Contains("hashed:p4", hashes);
		Assert.All(loaded.PasswordHistory, h =>
		{
			Assert.Equal("realm-a", h.RealmId);
			Assert.Equal(accountId, h.UserAccountId);
		});

		var rawReasons = await read.Database
			.SqlQueryRaw<string>(
				"""
				SELECT "Reason" AS "Value"
				FROM "UserAccountPasswordHistory"
				WHERE "UserAccountId" = {0}
				""",
				accountId)
			.ToListAsync();

		Assert.All(rawReasons, reason => Assert.Equal(nameof(PasswordChangeReason.Change), reason));
	}

	[Fact]
	public async Task UserAccountActionToken_StoresPurposeAndRevocationReason_AsStrings()
	{
		await using var provider = BuildProvider();
		long accountId;

		await using (var write = NewContext(provider))
		{
			var account = NewAccount();
			write.UserAccounts.Add(account);
			await write.SaveChangesAsync();
			accountId = account.Id;

			var token = UserAccountActionToken.Issue(
				account,
				ActionTokenPurpose.ChangeExpiredPassword,
				"token-hash",
				account.SecurityStamp.Value,
				Now,
				Now.AddMinutes(10));
			token.Revoke(ActionTokenRevocationReason.Superseded, Now.AddMinutes(1));

			write.UserAccountActionTokens.Add(token);
			await write.SaveChangesAsync();
		}

		await using var read = NewContext(provider);
		var rawPurpose = await read.Database
			.SqlQueryRaw<string>(
				"""
				SELECT "Purpose" AS "Value"
				FROM "UserAccountActionTokens"
				WHERE "UserAccountId" = {0}
				""",
				accountId)
			.SingleAsync();
		var rawRevokedReason = await read.Database
			.SqlQueryRaw<string>(
				"""
				SELECT "RevokedReason" AS "Value"
				FROM "UserAccountActionTokens"
				WHERE "UserAccountId" = {0}
				""",
				accountId)
			.SingleAsync();

		Assert.Equal(nameof(ActionTokenPurpose.ChangeExpiredPassword), rawPurpose);
		Assert.Equal(nameof(ActionTokenRevocationReason.Superseded), rawRevokedReason);
	}

	[Fact]
	public async Task PropertyScope_RoundTrips_WithVersionsDefinitionsAndActiveVersion()
	{
		await using var provider = BuildProvider();
		long scopeId;

		await using (var write = NewContext(provider))
		{
			var scope = NewActiveProfileScope("nickname", new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Text,
				DisplayName = "Nickname",
				IsRequired = true,
				IsCollection = true,
				ValidationRules = new PropertyValidationRules(
					minLength: 2,
					maxLength: 16,
					range: new PropertyRangeRule("a", "z", includeMin: true, includeMax: false),
					allowedValues: ["ally", "sam"],
					regexPattern: "^[a-z]+$",
					minItems: 1,
					maxItems: 3,
					customValidators: [new PropertyCustomValidationRef("reject-value", "BLOCK")])
			});

			write.PropertyScopes.Add(scope);
			await write.SaveChangesAsync();
			scopeId = scope.Id;
		}

		await using var read = NewContext(provider);
		var loaded = await read.PropertyScopes
			.Include("VersionItems.DefinitionVersionItems")
			.Include("DefinitionItems")
			.SingleAsync(s => s.Id == scopeId);

		Assert.NotNull(loaded.ActiveVersion);
		Assert.Equal(loaded.ActiveVersion!.Id, loaded.ActiveVersionId);
		Assert.Equal(PropertyScopeVersionStatus.Active, loaded.ActiveVersion.Status);

		var definitionVersion = loaded.ActiveVersion.DefinitionVersions.Single();
		Assert.Equal("nickname", definitionVersion.ClaimType);
		Assert.Equal(PropertyValueType.Text, definitionVersion.ValueType);
		Assert.True(definitionVersion.IsRequired);
		Assert.True(definitionVersion.IsCollection);
		Assert.True(definitionVersion.IsActive);

		var rules = definitionVersion.ValidationRules;
		Assert.Equal(2, rules.MinLength);
		Assert.Equal(16, rules.MaxLength);
		Assert.NotNull(rules.Range);
		Assert.Equal("a", rules.Range!.Min);
		Assert.Equal("z", rules.Range.Max);
		Assert.True(rules.Range.IncludeMin);
		Assert.False(rules.Range.IncludeMax);
		Assert.Equal(["ally", "sam"], rules.AllowedValues);
		Assert.Equal("^[a-z]+$", rules.RegexPattern);
		Assert.Equal(1, rules.MinItems);
		Assert.Equal(3, rules.MaxItems);
		Assert.Single(rules.CustomValidators, c => c.ValidatorKey == "reject-value" && c.ParametersJson == "BLOCK");

		Assert.Single(loaded.Definitions, d => d.ClaimType == "nickname");
	}

	[Fact]
	public async Task PropertyDefinitionVersion_ClaimType_IsQueryable_WithoutNavigationInclude()
	{
		await using var provider = BuildProvider();

		await using (var write = NewContext(provider))
		{
			write.PropertyScopes.Add(NewActiveProfileScope("birthdate", new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Date
			}));
			await write.SaveChangesAsync();
		}

		await using var read = NewContext(provider);
		var version = await read.Set<PropertyDefinitionVersion>()
			.AsNoTracking()
			.SingleAsync(dv => dv.ClaimType == "birthdate");

		Assert.Equal("birthdate", version.ClaimType);
		Assert.Null(version.PropertyDefinition);
	}

	[Fact]
	public async Task PropertyScope_CreateDraftVersion_AfterRoundTrip_CopiesActiveDefinitions()
	{
		await using var provider = BuildProvider();
		long scopeId;

		await using (var write = NewContext(provider))
		{
			var scope = NewActiveProfileScope("nickname", new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Text,
				DisplayName = "Nickname",
				IsRequired = true,
				IsCollection = true
			});

			write.PropertyScopes.Add(scope);
			await write.SaveChangesAsync();
			scopeId = scope.Id;
		}

		await using (var update = NewContext(provider))
		{
			var loaded = await update.PropertyScopes
				.Include("VersionItems.DefinitionVersionItems")
				.Include("DefinitionItems")
				.SingleAsync(s => s.Id == scopeId);

			var result = loaded.CreateDraftVersion("Profile v2", Now.AddMinutes(2));

			Assert.True(result.IsSuccess);
			await update.SaveChangesAsync();
		}

		await using var read = NewContext(provider);
		var reloaded = await read.PropertyScopes
			.Include("VersionItems.DefinitionVersionItems")
			.Include("DefinitionItems")
			.SingleAsync(s => s.Id == scopeId);

		var draft = reloaded.Versions.Single(v => v.Status == PropertyScopeVersionStatus.Draft);
		var definitionVersion = draft.DefinitionVersions.Single();

		Assert.Equal(2, draft.Version);
		Assert.Equal("nickname", definitionVersion.ClaimType);
		Assert.Equal(PropertyValueType.Text, definitionVersion.ValueType);
		Assert.Equal("Nickname", definitionVersion.DisplayName);
		Assert.True(definitionVersion.IsRequired);
		Assert.True(definitionVersion.IsCollection);
		Assert.True(definitionVersion.IsActive);
		Assert.Single(reloaded.Definitions, d => d.ClaimType == "nickname");
	}

	[Fact]
	public async Task PropertyScope_CreateDraftVersion_ReturnsProblem_WhenStableDefinitionsAreNotLoaded()
	{
		await using var provider = BuildProvider();
		long scopeId;

		await using (var write = NewContext(provider))
		{
			var scope = NewActiveProfileScope("nickname", new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Text
			});

			write.PropertyScopes.Add(scope);
			await write.SaveChangesAsync();
			scopeId = scope.Id;
		}

		await using var read = NewContext(provider);
		var loaded = await read.PropertyScopes
			.Include("VersionItems.DefinitionVersionItems")
			.SingleAsync(s => s.Id == scopeId);

		var result = loaded.CreateDraftVersion("Profile v2", Now.AddMinutes(2));

		Assert.True(result.IsFailure);
	}

	[Fact]
	public async Task UserAccountPropertyValues_RoundTrip_ByDefinition()
	{
		await using var provider = BuildProvider();
		long accountId;

		await using (var write = NewContext(provider))
		{
			var scope = NewActiveProfileScope("level", new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Integer,
				IsCollection = true
			});
			write.PropertyScopes.Add(scope);
			await write.SaveChangesAsync();

			var definitionVersion = scope.ActiveVersion!.DefinitionVersions.Single();
			var account = NewAccount();
			var service = new UserAccountPropertyValueService();
			Assert.True((await service.SetValuesAsync(account, definitionVersion, ["1", "3"], Now)).IsSuccess);

			write.UserAccounts.Add(account);
			await write.SaveChangesAsync();
			accountId = account.Id;
		}

		await using var read = NewContext(provider);
		var values = await read.Set<UserAccountPropertyValue>()
			.AsNoTracking()
			.Where(v => v.UserAccountId == accountId)
			.OrderBy(v => v.Ordinal)
			.ToListAsync();

		Assert.Equal(2, values.Count);
		Assert.All(values, v => Assert.Equal("level", v.ClaimType));
		Assert.All(values, v => Assert.Equal(PropertyValueType.Integer, v.ValueType));
		Assert.Equal("1", values[0].Value);
		Assert.Equal(0, values[0].Ordinal);
		Assert.Equal("3", values[1].Value);
		Assert.Equal(1, values[1].Ordinal);
	}

	[Fact]
	public async Task DuplicateSubjectId_InSameRealm_ViolatesUniqueIndex()
	{
		await using var provider = BuildProvider();

		await using var ctx = NewContext(provider);
		ctx.UserAccounts.Add(new UserAccount("realm-a", "dup-subject", "alice", "ALICE", "Alice", Now));
		ctx.UserAccounts.Add(new UserAccount("realm-a", "dup-subject", "bob", "BOB", "Bob", Now));

		await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task DuplicateNormalizedUsername_InSameRealm_ViolatesUniqueIndex()
	{
		await using var provider = BuildProvider();

		await using var ctx = NewContext(provider);
		ctx.UserAccounts.Add(new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now));
		ctx.UserAccounts.Add(new UserAccount("realm-a", "subject-2", "alice", "ALICE", "Alice 2", Now));

		await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task SameSubjectId_InDifferentRealms_IsAllowed()
	{
		await using var provider = BuildProvider();

		await using var ctx = NewContext(provider);
		ctx.UserAccounts.Add(new UserAccount("realm-a", "shared-subject", "alice", "ALICE", "Alice", Now));
		ctx.UserAccounts.Add(new UserAccount("realm-b", "shared-subject", "alice", "ALICE", "Alice", Now));

		var affected = await ctx.SaveChangesAsync();

		Assert.True(affected > 0);
	}

	[Fact]
	public async Task DuplicatePrimaryEmail_ForSameAccount_ViolatesSqliteProviderIndex()
	{
		await using var provider = BuildProvider();
		long accountId;

		await using (var write = NewContext(provider))
		{
			var account = NewAccount();
			write.UserAccounts.Add(account);
			await write.SaveChangesAsync();
			accountId = account.Id;
		}

		await using var ctx = NewContext(provider);
		await ctx.Database.ExecuteSqlRawAsync(
			"""
			INSERT INTO "UserAccountEmails"
				("RealmId", "UserAccountId", "Address", "NormalizedAddress", "IsPrimary", "IsVerified", "IsFictitious")
			VALUES
				({0}, {1}, {2}, {3}, 1, 1, 0)
			""",
			"realm-a",
			accountId,
			"alice@example.com",
			"ALICE@EXAMPLE.COM");

		var exception = await Assert.ThrowsAsync<SqliteException>(() =>
			ctx.Database.ExecuteSqlRawAsync(
				"""
				INSERT INTO "UserAccountEmails"
					("RealmId", "UserAccountId", "Address", "NormalizedAddress", "IsPrimary", "IsVerified", "IsFictitious")
				VALUES
					({0}, {1}, {2}, {3}, 1, 1, 0)
				""",
				"realm-a",
				accountId,
				"alice.alt@example.com",
				"ALICE.ALT@EXAMPLE.COM"));

		Assert.Equal(19, exception.SqliteErrorCode);
	}

	[Fact]
	public async Task DuplicatePropertyScopeName_InSameRealm_ViolatesUniqueIndex()
	{
		await using var provider = BuildProvider();

		await using var ctx = NewContext(provider);
		ctx.PropertyScopes.Add(new PropertyScope("realm-a", "profile", "Profile", Now));
		ctx.PropertyScopes.Add(new PropertyScope("realm-a", "profile", "Profile 2", Now));

		await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task DuplicatePropertyDefinitionClaimType_InSameRealm_ViolatesUniqueIndex()
	{
		await using var provider = BuildProvider();

		await using var ctx = NewContext(provider);
		ctx.PropertyScopes.Add(NewActiveScope("profile", "nickname", new PropertyDefinitionSettings
		{
			ValueType = PropertyValueType.Text
		}));
		ctx.PropertyScopes.Add(NewActiveScope("custom-profile", "nickname", new PropertyDefinitionSettings
		{
			ValueType = PropertyValueType.Text
		}));

		await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task WorkContext_PersistsAndFinds_Account()
	{
		await using var provider = BuildProvider();
		long accountId;

		using (var scope = provider.CreateScope())
		{
			var work = scope.ServiceProvider.GetRequiredService<IWorkContext>();
			var account = NewAccount();
			work.Add(account);
			await work.SaveAsync();
			accountId = account.Id;
		}

		using (var scope = provider.CreateScope())
		{
			var work = scope.ServiceProvider.GetRequiredService<IWorkContext>();
			var found = await work.FindAsync<UserAccount>(accountId);

			Assert.NotNull(found);
			Assert.Equal("subject-1", found!.SubjectId);
		}
	}

	[Fact]
	public async Task UserAccount_Version_RejectsConcurrentUpdates()
	{
		await using var provider = BuildProvider();
		long accountId;

		await using (var seed = NewContext(provider))
		{
			var account = NewAccount();
			seed.UserAccounts.Add(account);
			await seed.SaveChangesAsync();
			accountId = account.Id;
		}

		await using var first = NewContext(provider);
		await using var second = NewContext(provider);
		var firstAccount = await first.UserAccounts.SingleAsync(a => a.Id == accountId);
		var secondAccount = await second.UserAccounts.SingleAsync(a => a.Id == accountId);

		Assert.True(firstAccount.ChangeDisplayName("Alice First", Now.AddMinutes(1)).IsSuccess);
		await first.SaveChangesAsync();

		Assert.True(secondAccount.ChangeDisplayName("Alice Second", Now.AddMinutes(2)).IsSuccess);
		await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddUserAccountsSqliteInMemory();
		var provider = services.BuildServiceProvider();

		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>().Database.EnsureCreated();

		return provider;
	}

	private static UserAccountsSqliteDbContext NewContext(ServiceProvider provider)
	{
		return provider.CreateScope().ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
	}

	private static UserAccount NewAccount()
	{
		return new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
	}

	private static PropertyScope NewActiveProfileScope(string claimType, PropertyDefinitionSettings settings)
	{
		return NewActiveScope("profile", claimType, settings);
	}

	private static PropertyScope NewActiveScope(string scopeName, string claimType, PropertyDefinitionSettings settings)
	{
		var scope = new PropertyScope("realm-a", scopeName, "Profile", Now);
		var version = scope.Versions.Single();
		Assert.True(scope.AddDefinition(version, claimType, settings).IsSuccess);
		Assert.True(scope.ApproveVersion(version, Now.AddMinutes(1)).IsSuccess);
		return scope;
	}
}
