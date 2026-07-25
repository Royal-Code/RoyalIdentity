using System.Security.Claims;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// A persisted <see cref="ClaimsPrincipal"/>: all of its identities, each under
/// <see cref="ClaimsIdentityPayload"/>. Materialization is fail-closed — a principal with no identity is an
/// incomplete payload, not an empty principal.
/// </summary>
public sealed class ClaimsPrincipalPayload
{
	public List<ClaimsIdentityPayload> Identities { get; set; } = [];

	public static ClaimsPrincipalPayload From(ClaimsPrincipal principal)
	{
		ArgumentNullException.ThrowIfNull(principal);

		return new ClaimsPrincipalPayload
		{
			Identities = [.. principal.Identities.Select(ClaimsIdentityPayload.From)],
		};
	}

	public ClaimsPrincipal ToClaimsPrincipal(string payloadName)
	{
		if (Identities.Count is 0)
			throw OperationalPayloadException.IncompletePayload(payloadName, "the subject has no identity");

		return new ClaimsPrincipal(Identities.Select(identity => identity.ToClaimsIdentity()));
	}
}
