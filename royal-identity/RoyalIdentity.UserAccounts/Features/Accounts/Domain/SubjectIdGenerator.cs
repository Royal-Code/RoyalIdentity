using System.Security.Cryptography;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Generates opaque subject identifiers for user accounts.
/// </summary>
public static class SubjectIdGenerator
{
	private const int DefaultByteLength = 32;

	/// <summary>
	/// Creates a URL-safe random subject identifier.
	/// </summary>
	/// <returns>A cryptographically random subject identifier.</returns>
	public static string Create()
	{
		var bytes = RandomNumberGenerator.GetBytes(DefaultByteLength);
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}
}
