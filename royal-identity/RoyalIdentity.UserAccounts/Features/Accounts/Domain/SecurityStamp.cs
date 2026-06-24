using RoyalIdentity.Security.Cryptography;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Opaque version of the account's security-sensitive state.
/// </summary>
public sealed record SecurityStamp
{
	private const int ByteLength = 32;

	private SecurityStamp(string value)
	{
		Value = value;
	}

	/// <summary>
	/// Gets the persisted stamp value.
	/// </summary>
	public string Value { get; }

	/// <summary>
	/// Creates a new cryptographically random stamp.
	/// </summary>
	public static SecurityStamp New()
		=> new(CryptoRandom.CreateUniqueId(ByteLength, OutputFormat.Base64Url));

	/// <summary>
	/// Rehydrates a persisted stamp value.
	/// </summary>
	/// <param name="value">The persisted stamp value.</param>
	public static SecurityStamp FromPersisted(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		return new SecurityStamp(value);
	}

	/// <inheritdoc />
	public override string ToString()
		=> Value;
}
