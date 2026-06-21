namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Default normalizer: trims surrounding whitespace and upper-cases with the invariant culture, matching the
/// normalized columns produced by the module's persistence indexes.
/// </summary>
public sealed class DefaultUserAccountNormalizer : IUserAccountNormalizer
{
	/// <inheritdoc />
	public string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

	/// <inheritdoc />
	public string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

	/// <inheritdoc />
	public string NormalizeRoleName(string roleName) => roleName.Trim().ToUpperInvariant();
}
