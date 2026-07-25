using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted graph of an <see cref="AccessToken"/> that has no queryable column of its own. Realm, client,
/// access-token type and the timestamps are NOT here: they live in relational columns and arrive through
/// <see cref="AccessTokenIdentity"/>, which is their single source of truth (plan DF9/DF36).
/// <para>
/// <see cref="Token"/> is written only when the token string differs from the <c>jti</c> — that is, for a JWT
/// persisted in <c>Full</c> mode. A reference token's bearer coincides with its <c>jti</c>, which is the
/// lookup argument, so it is never copied into the payload (plan DF13/DF38).
/// </para>
/// <para>
/// Every collection is <c>required</c>: an omitted member must fail closed, not materialize as an empty
/// collection that silently drops audiences, resource URIs or claims.
/// </para>
/// </summary>
public sealed class AccessTokenPayload
{
    public required string Issuer { get; set; }

    public required string TokenType { get; set; }

    public string? Confirmation { get; set; }

    /// <summary>The compact JWT, present only in <c>Full</c> mode; <c>null</c> otherwise.</summary>
    public string? Token { get; set; }

    public required List<string> Audiences { get; set; }

    public required List<string> AllowedSigningAlgorithms { get; set; }

    public required List<string> ResourceUris { get; set; }

    public required List<ClaimPayload> Claims { get; set; }
}
