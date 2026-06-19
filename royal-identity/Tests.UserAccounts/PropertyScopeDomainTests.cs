using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

public class PropertyScopeDomainTests
{
	private static readonly DateTimeOffset Now = new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);

	[Fact]
	public void VersionApproval_CopiesActiveVersion_AndDoesNotAffectActiveUntilApproval()
	{
		var scope = new PropertyScope("realm-a", "profile", "Profile", Now);
		var initialDraft = scope.Versions.Single();
		var initialVersion = AddDefinition(
			scope,
			initialDraft,
			"birthdate",
			new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Date,
				DisplayName = "Birth date",
				IsRequired = false
			});

		Assert.True(scope.ApproveVersion(initialDraft, Now.AddMinutes(1)).IsSuccess);

		Assert.True(scope.CreateDraftVersion("Profile v2", Now.AddMinutes(2)).IsSuccess);
		var nextDraft = scope.Versions.Single(r => r.Status is PropertyScopeVersionStatus.Draft);
		var copiedVersion = nextDraft.DefinitionVersions.Single();

		Assert.True(scope.UpdateDefinition(
			nextDraft,
			"birthdate",
			new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Date,
				DisplayName = "Date of birth",
				IsRequired = true
			}).IsSuccess);

		Assert.Equal(PropertyScopeVersionStatus.Active, initialDraft.Status);
		Assert.Equal("Birth date", initialVersion.DisplayName);
		Assert.False(initialVersion.IsRequired);
		Assert.Equal("Date of birth", copiedVersion.DisplayName);
		Assert.True(copiedVersion.IsRequired);

		Assert.True(scope.ApproveVersion(nextDraft, Now.AddMinutes(3)).IsSuccess);

		Assert.Equal(PropertyScopeVersionStatus.Archived, initialDraft.Status);
		Assert.Equal(PropertyScopeVersionStatus.Active, nextDraft.Status);
		Assert.Equal(copiedVersion, scope.ActiveVersion?.DefinitionVersions.Single());
	}

	[Fact]
	public async Task Inactivation_PreservesValues_AndSuppressesProjection()
	{
		var account = CreateAccount();
		var scope = CreateActiveProfileScope("nickname", new PropertyDefinitionSettings());
		var definition = scope.ActiveVersion!.DefinitionVersions.Single();
		var service = new UserAccountPropertyValueService();

		var setResult = await service.SetValuesAsync(account, definition, ["ally"], Now.AddMinutes(1));

		Assert.True(setResult.IsSuccess);
		Assert.Single(account.PropertyValues);

		var projector = new UserAccountClaimProjector();
		var projected = projector.Project(
			account,
			new UserAccountsRealmOptions(),
			[scope],
			["profile"],
			["nickname"]);

		Assert.Single(projected, v => v.ClaimType == "nickname" && v.Value == "ally");

		Assert.True(scope.CreateDraftVersion("Profile v2", Now.AddMinutes(2)).IsSuccess);
		var nextDraft = scope.Versions.Single(r => r.Status is PropertyScopeVersionStatus.Draft);
		Assert.True(scope.DeactivateDefinition(nextDraft, "nickname").IsSuccess);

		projected = projector.Project(account, new UserAccountsRealmOptions(), [scope], ["profile"], ["nickname"]);

		Assert.Single(projected, v => v.ClaimType == "nickname" && v.Value == "ally");
		Assert.True(scope.ApproveVersion(nextDraft, Now.AddMinutes(3)).IsSuccess);
		projected = projector.Project(account, new UserAccountsRealmOptions(), [scope], ["profile"], ["nickname"]);

		Assert.DoesNotContain(projected, v => v.ClaimType == "nickname");
		Assert.Single(account.PropertyValues);

		scope.Deactivate();
		projected = projector.Project(account, new UserAccountsRealmOptions(), [scope], ["profile"], ["nickname"]);

		Assert.DoesNotContain(projected, v => v.ClaimType == "nickname");
		Assert.Single(account.PropertyValues);
	}

	[Fact]
	public void ActiveVersions_CannotBeEditedDirectly()
	{
		var scope = CreateActiveProfileScope("nickname", new PropertyDefinitionSettings());
		var activeVersion = scope.ActiveVersion!;

		var addResult = scope.AddDefinition(activeVersion, "birthdate", new PropertyDefinitionSettings());
		var updateResult = scope.UpdateDefinition(
			activeVersion,
			"nickname",
			new PropertyDefinitionSettings
			{
				DisplayName = "Nick"
			});
		var deactivateResult = scope.DeactivateDefinition(activeVersion, "nickname");

		Assert.True(addResult.IsFailure);
		Assert.True(updateResult.IsFailure);
		Assert.True(deactivateResult.IsFailure);
	}

	[Fact]
	public async Task PropertyValueService_ValidatesTextRulesAndCustomValidatorsBeforeAssignment()
	{
		var account = CreateAccount();
		var scope = CreateActiveProfileScope(
			"nickname",
			new PropertyDefinitionSettings
			{
				ValidationRules = new PropertyValidationRules(
					minLength: 2,
					maxLength: 8,
					regexPattern: "^[A-Z]+$",
					customValidators:
					[
						new PropertyCustomValidationRef("reject-value", "BLOCK")
					])
			});
		var definition = scope.ActiveVersion!.DefinitionVersions.Single();
		var service = new UserAccountPropertyValueService([new RejectValueValidator()]);

		var tooShort = await service.SetValuesAsync(account, definition, ["A"], Now.AddMinutes(1));
		var regexMismatch = await service.SetValuesAsync(account, definition, ["abc"], Now.AddMinutes(2));
		var customRejected = await service.SetValuesAsync(account, definition, ["BLOCK"], Now.AddMinutes(3));
		var valid = await service.SetValuesAsync(account, definition, ["ALLY"], Now.AddMinutes(4));

		Assert.True(tooShort.IsFailure);
		Assert.True(regexMismatch.IsFailure);
		Assert.True(customRejected.IsFailure);
		Assert.True(valid.IsSuccess);
		Assert.Single(account.PropertyValues, v => v.ClaimType == "nickname" && v.Value == "ALLY");
	}

	[Fact]
	public async Task PropertyValueService_ValidatesTypedRangeAllowedValuesAndCollectionCardinality()
	{
		var account = CreateAccount();
		var dateScope = CreateActiveProfileScope(
			"birthdate",
			new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Date,
				ValidationRules = new PropertyValidationRules(
					range: new PropertyRangeRule("1900-01-01", "2020-12-31"))
			});
		var dateDefinition = dateScope.ActiveVersion!.DefinitionVersions.Single();
		var dateService = new UserAccountPropertyValueService();

		var belowDateRange = await dateService.SetValuesAsync(account, dateDefinition, ["1800-01-01"], Now.AddMinutes(1));
		var validDate = await dateService.SetValuesAsync(account, dateDefinition, ["2000-05-10"], Now.AddMinutes(2));

		Assert.True(belowDateRange.IsFailure);
		Assert.True(validDate.IsSuccess);
		Assert.Single(account.PropertyValues, v => v.ClaimType == "birthdate" && v.Value == "2000-05-10");
		Assert.Same(
			dateDefinition.PropertyDefinition,
			account.PropertyValues.Single(v => v.ClaimType == "birthdate").PropertyDefinition);

		var numberScope = CreateActiveProfileScope(
			"level",
			new PropertyDefinitionSettings
			{
				ValueType = PropertyValueType.Integer,
				IsCollection = true,
				ValidationRules = new PropertyValidationRules(
					allowedValues: ["1", "2", "3"],
					minItems: 2,
					maxItems: 3)
			});
		var numberDefinition = numberScope.ActiveVersion!.DefinitionVersions.Single();

		var tooFew = await dateService.SetValuesAsync(account, numberDefinition, ["1"], Now.AddMinutes(3));
		var notAllowed = await dateService.SetValuesAsync(account, numberDefinition, ["1", "4"], Now.AddMinutes(4));
		var validNumbers = await dateService.SetValuesAsync(account, numberDefinition, ["1", "3"], Now.AddMinutes(5));

		Assert.True(tooFew.IsFailure);
		Assert.True(notAllowed.IsFailure);
		Assert.True(validNumbers.IsSuccess);
		Assert.Contains(account.PropertyValues, v => v.ClaimType == "level" && v.Value == "1" && v.Ordinal == 0);
		Assert.Contains(account.PropertyValues, v => v.ClaimType == "level" && v.Value == "3" && v.Ordinal == 1);
	}

	[Fact]
	public void ValidateClaimProjectionConfiguration_DetectsFixedDynamicClaimTypeCollision()
	{
		var scope = CreateActiveProfileScope("email", new PropertyDefinitionSettings());
		var validator = new ValidateClaimProjectionConfiguration();

		var errors = validator.Validate(new UserAccountsRealmOptions(), scope.Definitions);

		Assert.Contains(errors, e => e.Contains("email", StringComparison.Ordinal));
	}

	[Fact]
	public async Task ClaimProjector_AppliesScopeAndClaimTypeIntersection_AndSkipsInactiveAccounts()
	{
		var account = CreateAccount();
		Assert.True(account.AddRole(new UserAccountRole("realm-a", "admin", "ADMIN"), Now).IsSuccess);
		Assert.True(account.AddEmail(
			new UserAccountEmail("realm-a", "alice@example.com", "ALICE@EXAMPLE.COM", true, true, false),
			Now).IsSuccess);

		var scope = CreateActiveProfileScope("nickname", new PropertyDefinitionSettings());
		var definition = scope.ActiveVersion!.DefinitionVersions.Single();
		var service = new UserAccountPropertyValueService();
		Assert.True((await service.SetValuesAsync(account, definition, ["ALLY"], Now.AddMinutes(1))).IsSuccess);

		var projector = new UserAccountClaimProjector();
		var projected = projector.Project(
			account,
			new UserAccountsRealmOptions(),
			[scope],
			["profile"],
			["name", "role", "nickname"]);

		Assert.Contains(projected, v => v.ScopeName == "profile" && v.ClaimType == "name" && v.Value == "Alice");
		Assert.Contains(projected, v => v.ScopeName == "profile" && v.ClaimType == "role" && v.Value == "admin");
		Assert.Contains(projected, v => v.ScopeName == "profile" && v.ClaimType == "nickname" && v.Value == "ALLY");
		Assert.DoesNotContain(projected, v => v.ClaimType == "email");

		account.Deactivate(Now.AddMinutes(2));

		Assert.Empty(projector.Project(account, new UserAccountsRealmOptions(), [scope], ["profile"], ["name"]));
	}

	private static PropertyScope CreateActiveProfileScope(
		string claimType,
		PropertyDefinitionSettings settings)
	{
		var scope = new PropertyScope("realm-a", "profile", "Profile", Now);
		var version = scope.Versions.Single();
		AddDefinition(scope, version, claimType, settings);
		Assert.True(scope.ApproveVersion(version, Now.AddMinutes(1)).IsSuccess);
		return scope;
	}

	private static PropertyDefinitionVersion AddDefinition(
		PropertyScope scope,
		PropertyScopeVersion version,
		string claimType,
		PropertyDefinitionSettings settings)
	{
		var result = scope.AddDefinition(version, claimType, settings);

		Assert.True(result.IsSuccess);
		return version.DefinitionVersions.Single(d => d.ClaimType == claimType);
	}

	private static UserAccount CreateAccount()
	{
		return new UserAccount(
			"realm-a",
			"subject-1",
			"alice",
			"ALICE",
			"Alice",
			Now);
	}

	private sealed class RejectValueValidator : IUserAccountPropertyValidator
	{
		public string Key => "reject-value";

		public ValueTask<Result> ValidateAsync(
			UserAccountPropertyValidationContext context,
			CancellationToken cancellationToken)
		{
			if (context.CanonicalValue == context.ParametersJson)
			{
				return ValueTask.FromResult<Result>(
					Problems.InvalidParameter("Value was rejected.", nameof(context.RawValue), "test.rejected"));
			}

			return ValueTask.FromResult(Result.Ok());
		}
	}
}
