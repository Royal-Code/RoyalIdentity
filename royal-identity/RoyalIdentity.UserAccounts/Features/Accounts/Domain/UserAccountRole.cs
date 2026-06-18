namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Role assigned directly to a user account.
/// </summary>
public class UserAccountRole
{
	private UserAccountRole()
	{
	}

	/// <summary>
	/// Creates an account role.
	/// </summary>
	/// <param name="name">The role name.</param>
	public UserAccountRole(string name)
	{
		Name = name.Trim();
		NormalizedName = Normalize(Name);
	}

	/// <summary>
	/// Gets the role name.
	/// </summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized role name used for comparisons.
	/// </summary>
	public string NormalizedName { get; private set; } = string.Empty;

	internal static string Normalize(string name)
	{
		return name.Trim().ToUpperInvariant();
	}
}
