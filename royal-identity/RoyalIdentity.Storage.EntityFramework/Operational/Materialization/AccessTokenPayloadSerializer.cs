using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of an <see cref="AccessToken"/> to a versioned payload and back
/// (plan DF9). Whatever a relational column holds is never written here and always comes back from
/// <see cref="AccessTokenIdentity"/>, so payload and columns cannot disagree. Materialization is independent:
/// it always produces a new graph, so mutating what a store returned never reaches the database without an
/// explicit operation.
/// </summary>
public sealed class AccessTokenPayloadSerializer
{
    /// <summary>Current payload schema version.</summary>
    public const int CurrentVersion = 1;

    private readonly OperationalPayloadCodec<AccessTokenPayload> codec =
        new(nameof(AccessToken), CurrentVersion);

    /// <param name="token">The token to serialize.</param>
    /// <param name="persistCompactToken">
    /// Whether the compact JWT belongs in the payload. Only <c>JwtAccessTokenPersistence.Full</c> asks for it;
    /// <c>Metadata</c> keeps the queryable graph without the bearer (plan DF31). A reference token never
    /// persists its token string either way, because it coincides with the <c>jti</c> lookup argument
    /// (plan DF13/DF38).
    /// </param>
    public (int Version, string Json) Serialize(AccessToken token, bool persistCompactToken = true)
    {
        ArgumentNullException.ThrowIfNull(token);

        var payload = new AccessTokenPayload
        {
            Issuer = token.Issuer,
            TokenType = token.TokenType,
            Confirmation = token.Confirmation,
            Token = persistCompactToken && !string.Equals(token.Token, token.Id, StringComparison.Ordinal)
                ? token.Token
                : null,
            Audiences = [.. token.Audiences],
            AllowedSigningAlgorithms = [.. token.AllowedSigningAlgorithms],
            ResourceUris = [.. token.ResourceUris],
            Claims = [.. token.Claims.Select(ClaimPayload.From)],
        };

        return (CurrentVersion, codec.Serialize(payload));
    }

    /// <param name="version">The persisted payload version.</param>
    /// <param name="json">The persisted payload.</param>
    /// <param name="identity">The authoritative relational identity of the row.</param>
    public AccessToken Deserialize(int version, string json, AccessTokenIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var payload = codec.Deserialize(version, json);

        var token = new AccessToken(
            identity.ClientId,
            payload.Issuer,
            identity.AccessTokenType,
            identity.CreatedAtUtc,
            identity.Lifetime,
            identity.Jti,
            payload.TokenType)
        {
            RealmId = identity.RealmId,
            Confirmation = payload.Confirmation,
            Audiences = [.. payload.Audiences],
            AllowedSigningAlgorithms = [.. payload.AllowedSigningAlgorithms],
        };

        if (payload.Token is not null)
            token.Token = payload.Token;

        foreach (var uri in payload.ResourceUris)
            token.ResourceUris.Add(uri);

        foreach (var claim in payload.Claims)
            token.Claims.Add(claim.ToClaim());

        return token;
    }
}
