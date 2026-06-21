using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;

/// <summary>
/// Reads and projects an account's claims for the requested identity scopes and claim types. This is the read
/// building block behind the IdP claims provider; it returns the module's internal claim values, leaving BCL
/// claim creation to the integration edge.
/// </summary>
public sealed class UserAccountClaimsReader(UserAccountReader reader, UserAccountClaimProjector projector)
{
	/// <summary>
	/// Projects the claims of an account, combining fixed fields, roles and dynamic property values by the
	/// intersection of requested identity scopes and allowed claim types. An inactive or missing account yields
	/// no claims.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="subjectId">The account subject identifier.</param>
	/// <param name="options">The realm account policies (fixed-field projections).</param>
	/// <param name="identityScopeNames">The requested identity scope names.</param>
	/// <param name="claimTypes">The allowed claim types.</param>
	/// <param name="ct">A cancellation token.</param>
	/// <returns>The projected internal claim values.</returns>
	public async Task<IReadOnlyList<AccountClaimValue>> GetClaimsAsync(
		string realmId,
		string subjectId,
		UserAccountsRealmOptions options,
		IEnumerable<string> identityScopeNames,
		IEnumerable<string> claimTypes,
		CancellationToken ct = default)
	{
		var scopeNames = identityScopeNames.Distinct(StringComparer.Ordinal).ToList();
		var allowedClaimTypes = claimTypes.Distinct(StringComparer.Ordinal).ToList();

		var account = await reader.FindBySubjectIdAsync(realmId, subjectId, ct);
		if (account is null || !account.IsActive)
		{
			return [];
		}

		var scopes = await reader.LoadActiveScopesAsync(realmId, scopeNames, ct);
		return projector.Project(account, options, scopes, scopeNames, allowedClaimTypes);
	}
}
