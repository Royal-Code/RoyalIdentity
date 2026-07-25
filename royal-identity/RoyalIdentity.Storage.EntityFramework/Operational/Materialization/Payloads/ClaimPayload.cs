using System.Security.Claims;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// <para>
///     The minimal persisted claim contract (plan DF34): <see cref="Type"/>, <see cref="Value"/> and
///     <see cref="ValueType"/>, the same three fields IdentityServer4 persisted as <c>ClaimLite</c>.
/// </para>
/// <para>
///     <see cref="Claim.Issuer"/>, <see cref="Claim.OriginalIssuer"/> and <see cref="Claim.Properties"/> are
///     deliberately outside the contract: they carry no operational semantics, and materialization recreates
///     the claim with the canonical/default issuer. This is an omission by decision, not by accident — a
///     future dependency on claim metadata requires a new explicit payload version, never a silent loss.
///     Properties belonging to other models (an authorization code's own <c>Properties</c>, for instance) are
///     unaffected and keep round-tripping.
/// </para>
/// </summary>
/// <param name="Type">The claim type.</param>
/// <param name="Value">The claim value.</param>
/// <param name="ValueType">The claim value type, when it is not the default string type.</param>
public sealed record ClaimPayload(string Type, string Value, string? ValueType)
{
	/// <summary>Captures a claim into the minimal contract.</summary>
	public static ClaimPayload From(Claim claim)
	{
		ArgumentNullException.ThrowIfNull(claim);

		return new ClaimPayload(
			claim.Type,
			claim.Value,
			string.Equals(claim.ValueType, ClaimValueTypes.String, StringComparison.Ordinal) ? null : claim.ValueType);
	}

	/// <summary>Recreates the claim with the canonical issuer.</summary>
	public Claim ToClaim() => new(Type, Value, ValueType ?? ClaimValueTypes.String);
}
