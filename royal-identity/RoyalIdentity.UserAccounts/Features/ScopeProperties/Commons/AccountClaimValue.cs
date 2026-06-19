namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;

/// <summary>
/// Internal claim-like projection used by the pure module before the integration adapter creates BCL claims.
/// </summary>
public sealed class AccountClaimValue
{
	/// <summary>
	/// Gets the identity scope name that enabled the value.
	/// </summary>
	public string ScopeName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the claim type.
	/// </summary>
	public string ClaimType { get; init; } = string.Empty;

	/// <summary>
	/// Gets the claim value.
	/// </summary>
	public string Value { get; init; } = string.Empty;
}
