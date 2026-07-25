using System.Security.Claims;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// A persisted identity of a <see cref="ClaimsPrincipal"/>: the authentication type, the claim types that
/// select name and role, and the claims themselves under the minimal contract of <see cref="ClaimPayload"/>.
/// </summary>
public sealed class ClaimsIdentityPayload
{
	public string? AuthenticationType { get; set; }

	public string? NameClaimType { get; set; }

	public string? RoleClaimType { get; set; }

	public required List<ClaimPayload> Claims { get; set; }

	public static ClaimsIdentityPayload From(ClaimsIdentity identity)
	{
		ArgumentNullException.ThrowIfNull(identity);

		return new ClaimsIdentityPayload
		{
			AuthenticationType = identity.AuthenticationType,
			NameClaimType = identity.NameClaimType,
			RoleClaimType = identity.RoleClaimType,
			Claims = [.. identity.Claims.Select(ClaimPayload.From)],
		};
	}

	public ClaimsIdentity ToClaimsIdentity() => new(
		Claims.Select(claim => claim.ToClaim()),
		AuthenticationType,
		NameClaimType,
		RoleClaimType);
}
