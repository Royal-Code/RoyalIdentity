namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Produces opaque OIDC subject identifiers for new accounts. Injected so the generation strategy is a single,
/// swappable home rather than a static utility.
/// </summary>
public interface ISubjectIdGenerator
{
	/// <summary>
	/// Creates a new unique subject identifier.
	/// </summary>
	/// <returns>A cryptographically random, URL-safe subject identifier.</returns>
	string NewSubjectId();
}
