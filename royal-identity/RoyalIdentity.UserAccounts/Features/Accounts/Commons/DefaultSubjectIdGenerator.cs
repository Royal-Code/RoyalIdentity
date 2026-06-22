using RoyalIdentity.Security.Cryptography;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// Default subject identifier generator producing a 256-bit URL-safe random value.
/// </summary>
public sealed class DefaultSubjectIdGenerator : ISubjectIdGenerator
{
	private const int ByteLength = 32;

	/// <inheritdoc />
	public string NewSubjectId()
		=> CryptoRandom.CreateUniqueId(ByteLength, OutputFormat.Base64Url);
}
