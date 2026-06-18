namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Email address owned by a user account.
/// </summary>
public class UserAccountEmail
{
	private UserAccountEmail()
	{
	}

	/// <summary>
	/// Creates an account email.
	/// </summary>
	/// <param name="address">The email address.</param>
	/// <param name="isPrimary">Whether this is the primary email.</param>
	/// <param name="isVerified">Whether this email is verified.</param>
	/// <param name="isFictitious">Whether this email is generated and not user-owned.</param>
	public UserAccountEmail(string address, bool isPrimary, bool isVerified, bool isFictitious)
	{
		Address = address.Trim();
		NormalizedAddress = Normalize(Address);
		IsPrimary = isPrimary;
		IsVerified = isVerified;
		IsFictitious = isFictitious;
	}

	/// <summary>
	/// Gets the email address as provided by the account flow.
	/// </summary>
	public string Address { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the normalized email address used for comparisons.
	/// </summary>
	public string NormalizedAddress { get; private set; } = string.Empty;

	/// <summary>
	/// Gets whether this is the account primary email.
	/// </summary>
	public bool IsPrimary { get; private set; }

	/// <summary>
	/// Gets whether this email has been verified.
	/// </summary>
	public bool IsVerified { get; private set; }

	/// <summary>
	/// Gets whether this email is fictitious.
	/// </summary>
	public bool IsFictitious { get; private set; }

	internal void MarkPrimary(bool primary)
	{
		IsPrimary = primary;
	}

	internal void MarkVerified()
	{
		IsVerified = true;
	}

	internal static string Normalize(string address)
	{
		return address.Trim().ToUpperInvariant();
	}
}
