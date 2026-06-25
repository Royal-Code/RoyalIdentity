using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;

/// <summary>
/// Projects account fields, roles, and dynamic property values into internal claim values.
/// </summary>
public class UserAccountClaimProjector
{
	/// <summary>
	/// Projects claims using requested identity scopes and allowed claim types.
	/// </summary>
	/// <param name="account">The account to project.</param>
	/// <param name="options">Realm account options.</param>
	/// <param name="propertyScopes">Dynamic property scopes.</param>
	/// <param name="identityScopeNames">Requested identity scope names.</param>
	/// <param name="claimTypes">Allowed claim types.</param>
	/// <returns>Internal claim values.</returns>
	public IReadOnlyList<AccountClaimValue> Project(
		UserAccount account,
		UserAccountsRealmOptions options,
		IEnumerable<PropertyScope> propertyScopes,
		IEnumerable<string> identityScopeNames,
		IEnumerable<string> claimTypes)
	{
		if (!account.IsActive)
		{
			return [];
		}

		var requestedScopes = identityScopeNames.ToHashSet(StringComparer.Ordinal);
		var requestedClaimTypes = claimTypes.ToHashSet(StringComparer.Ordinal);
		List<AccountClaimValue> values = [];

		ProjectFixedFields(account, options, requestedScopes, requestedClaimTypes, values);
		ProjectDynamicProperties(account, propertyScopes, requestedScopes, requestedClaimTypes, values);

		return values;
	}

	private static void ProjectFixedFields(
		UserAccount account,
		UserAccountsRealmOptions options,
		ISet<string> requestedScopes,
		ISet<string> requestedClaimTypes,
		ICollection<AccountClaimValue> values)
	{
		foreach (var projection in options.FixedFieldClaimProjections.Where(p => p.Include))
		{
			if (!options.EnablePhoneNumber &&
				projection.Field is FixedAccountField.PrimaryPhone or FixedAccountField.PhoneVerified)
			{
				continue;
			}

			if (!requestedScopes.Contains(projection.ScopeName) ||
				!requestedClaimTypes.Contains(projection.ClaimType))
			{
				continue;
			}

			switch (projection.Field)
			{
				case FixedAccountField.Username:
					Add(values, projection.ScopeName, projection.ClaimType, account.Username);
					break;

				case FixedAccountField.DisplayName:
					Add(values, projection.ScopeName, projection.ClaimType, account.DisplayName);
					break;

				case FixedAccountField.PrimaryEmail:
					if (account.PrimaryEmail is not null)
					{
						Add(values, projection.ScopeName, projection.ClaimType, account.PrimaryEmail.Address);
					}

					break;

				case FixedAccountField.EmailVerified:
					if (account.PrimaryEmail is not null)
					{
						Add(values, projection.ScopeName, projection.ClaimType, account.PrimaryEmail.IsVerified ? "true" : "false");
					}

					break;

				case FixedAccountField.PrimaryPhone:
					if (account.PrimaryPhone is not null)
					{
						Add(values, projection.ScopeName, projection.ClaimType, account.PrimaryPhone.Number);
					}

					break;

				case FixedAccountField.PhoneVerified:
					if (account.PrimaryPhone is not null)
					{
						Add(values, projection.ScopeName, projection.ClaimType, account.PrimaryPhone.IsVerified ? "true" : "false");
					}

					break;

				case FixedAccountField.Roles:
					foreach (var role in account.Roles)
					{
						Add(values, projection.ScopeName, projection.ClaimType, role.Name);
					}

					break;

				case FixedAccountField.ExternalId:
					if (!string.IsNullOrWhiteSpace(account.ExternalId))
					{
						Add(values, projection.ScopeName, projection.ClaimType, account.ExternalId);
					}

					break;
			}
		}
	}

	private static void ProjectDynamicProperties(
		UserAccount account,
		IEnumerable<PropertyScope> propertyScopes,
		ISet<string> requestedScopes,
		ISet<string> requestedClaimTypes,
		ICollection<AccountClaimValue> values)
	{
		foreach (var scope in propertyScopes.Where(s => s.IsActive && requestedScopes.Contains(s.Name)))
		{
			var activeVersion = scope.ActiveVersion;
			if (activeVersion is null)
			{
				continue;
			}

			var activeClaimTypes = activeVersion.DefinitionVersions
				.Where(d => d.IsActive && requestedClaimTypes.Contains(d.ClaimType))
				.Select(d => d.ClaimType)
				.ToHashSet(StringComparer.Ordinal);

			foreach (var propertyValue in account.PropertyValues
				.Where(v => activeClaimTypes.Contains(v.ClaimType))
				.OrderBy(v => v.Ordinal))
			{
				Add(values, scope.Name, propertyValue.ClaimType, propertyValue.Value);
			}
		}
	}

	private static void Add(
		ICollection<AccountClaimValue> values,
		string scopeName,
		string claimType,
		string value)
	{
		values.Add(new AccountClaimValue
		{
			ScopeName = scopeName,
			ClaimType = claimType,
			Value = value
		});
	}
}
