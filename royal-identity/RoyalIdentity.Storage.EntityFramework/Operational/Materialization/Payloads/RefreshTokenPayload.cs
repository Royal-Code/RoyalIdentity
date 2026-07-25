using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted graph of a <see cref="RefreshToken"/> that has no queryable column of its own. Realm, client
/// and the timestamps arrive through <see cref="RefreshTokenIdentity"/> (plan DF9/DF36); consumption state,
/// state version and claims mode are columns too. The raw handle is the lookup argument and is never persisted
/// (plan DF38), and no identifier of the previous access token exists here: its row is not a dependency of a
/// refresh (plan DF41).
/// </summary>
public sealed class RefreshTokenPayload
{
    public required string Issuer { get; set; }

    public string? Confirmation { get; set; }

    public required List<string> RequestedScopes { get; set; }

    public required List<string> ResourceUris { get; set; }

    public required List<string> Audiences { get; set; }

    public required List<string> AllowedSigningAlgorithms { get; set; }

    public required List<ClaimPayload> Claims { get; set; }
}
