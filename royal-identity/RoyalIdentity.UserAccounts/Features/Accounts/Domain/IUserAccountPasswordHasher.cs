namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Hashing seam used by the pure account domain for local credentials.
/// </summary>
public interface IUserAccountPasswordHasher
{
	/// <summary>
	/// Creates a password hash for storage.
	/// </summary>
	/// <param name="password">The plain password supplied by the caller.</param>
	/// <returns>The stored password hash.</returns>
	string Hash(string password);

	/// <summary>
	/// Verifies a plain password against a stored hash.
	/// </summary>
	/// <param name="password">The plain password supplied by the caller.</param>
	/// <param name="passwordHash">The stored password hash.</param>
	/// <returns><c>true</c> when the password matches the hash.</returns>
	bool Verify(string password, string passwordHash);
}
