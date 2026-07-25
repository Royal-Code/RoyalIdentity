using System.Security.Cryptography;
using System.Text;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// <para>
///     Computes the persisted lookup key of a handle-addressed operational record (plan DF38): authorization
///     codes, refresh tokens, authorize-parameters handles and the bearer of a reference access token are
///     located by a deterministic SHA-256 digest, and the raw value is never persisted — not in the key, not
///     in a projected column and not in the payload.
/// </para>
/// <para>
///     The digest is domain-separated by record type, so the same string in two record types never produces
///     the same key. The realm is part of the row identity, not of the digest, so a realm change is a
///     different row rather than a different hash of the same handle.
/// </para>
/// <para>
///     HMAC is deliberately not used: the handles covered here are generated with high entropy, so a keyed
///     digest would add key distribution and rotation to the critical path without a matching gain — the same
///     reasoning IdentityServer4 applied to its grant handles.
/// </para>
/// </summary>
public sealed class OperationalLookupDigest
{
	/// <summary>
	/// The digest of <paramref name="value"/> within <paramref name="recordType"/>, as lowercase hex.
	/// </summary>
	/// <param name="recordType">The record type; see <see cref="OperationalRecordTypes"/>.</param>
	/// <param name="value">
	/// The raw handle. For an access token this is always the <c>jti</c> (plan DF13): the reference bearer
	/// coincides with it today, and a compact JWT is never a lookup key.
	/// </param>
	public string Compute(string recordType, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(recordType);
		ArgumentException.ThrowIfNullOrEmpty(value);

		var bytes = Encoding.UTF8.GetBytes($"{recordType}|{value}");

		return Convert.ToHexStringLower(SHA256.HashData(bytes));
	}
}
