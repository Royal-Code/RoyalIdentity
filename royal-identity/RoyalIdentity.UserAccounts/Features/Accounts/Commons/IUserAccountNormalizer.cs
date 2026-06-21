namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Single home for value normalization used by every account use case. Features must not normalize on their
/// own: lookups and uniqueness constraints depend on a consistent normalized form across the whole module.
/// </summary>
public interface IUserAccountNormalizer
{
	/// <summary>
	/// Normalizes a username for case-insensitive lookup and uniqueness.
	/// </summary>
	/// <param name="username">The raw username.</param>
	/// <returns>The normalized username.</returns>
	string NormalizeUsername(string username);

	/// <summary>
	/// Normalizes an email address for case-insensitive lookup and uniqueness.
	/// </summary>
	/// <param name="email">The raw email address.</param>
	/// <returns>The normalized email address.</returns>
	string NormalizeEmail(string email);

	/// <summary>
	/// Normalizes a role name for case-insensitive lookup and uniqueness.
	/// </summary>
	/// <param name="roleName">The raw role name.</param>
	/// <returns>The normalized role name.</returns>
	string NormalizeRoleName(string roleName);
}
