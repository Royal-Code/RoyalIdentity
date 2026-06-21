using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.UseCases;

/// <summary>
/// Sets (replaces) the dynamic property values of an account for one claim type, validating against the scope's
/// active version. Fails when the scope is missing, has no active version, does not define the claim type in its
/// active version, or is inactive. Intended for seeds and tests.
/// </summary>
public partial class SetUserAccountScopeProperty
{
	/// <summary>
	/// Gets or sets the owning realm.
	/// </summary>
	public string RealmId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the subject identifier of the target account.
	/// </summary>
	public string SubjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the identity scope name that owns the property.
	/// </summary>
	public string ScopeName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the property claim type.
	/// </summary>
	public string ClaimType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the raw values to assign. An empty list clears the property when it is not required.
	/// </summary>
	public IReadOnlyList<string> Values { get; set; } = [];

	/// <summary>
	/// Validates the set-property input.
	/// </summary>
	/// <param name="problems">The collected problems, when invalid.</param>
	/// <returns><c>true</c> when the input is invalid.</returns>
	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<SetUserAccountScopeProperty>()
			.NotEmpty(RealmId)
			.NotEmpty(SubjectId)
			.NotEmpty(ScopeName)
			.NotEmpty(ClaimType)
			.NotNull(Values)
			.HasProblems(out problems);
	}

	/// <summary>
	/// Executes the set-property use case.
	/// </summary>
	[Command, WithValidateModel, WithWorkContext]
	public async Task<Result> Execute(
		UserAccountReader reader,
		UserAccountPropertyValueService valueService,
		TimeProvider clock,
		CancellationToken ct)
	{
		var account = await reader.FindBySubjectIdAsync(RealmId, SubjectId, ct);
		if (account is null)
		{
			return Problems.NotFound("Account was not found in the realm.", nameof(SubjectId), "user_account.not_found");
		}

		var scope = await reader.FindScopeByNameAsync(RealmId, ScopeName, ct);
		if (scope is null)
		{
			return Problems.NotFound("Property scope was not found in the realm.", nameof(ScopeName), "user_account.property_scope_not_found");
		}

		var activeVersion = scope.ActiveVersion;
		if (activeVersion is null)
		{
			return Problems.InvalidState("Property scope has no active version.", nameof(ScopeName), "user_account.property_scope_no_active_version");
		}

		var definitionVersion = activeVersion.DefinitionVersions.FirstOrDefault(d => d.ClaimType == ClaimType);
		if (definitionVersion is null)
		{
			return Problems.NotFound("Property is not defined in the scope active version.", nameof(ClaimType), "user_account.property_not_defined");
		}

		return await valueService.SetValuesAsync(account, definitionVersion, Values, clock.GetUtcNow(), ct);
	}
}
