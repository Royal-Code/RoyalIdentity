using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// <para>
///     The relational identity of a persisted artifact: the values that live in queryable columns and are, by
///     definition, the authoritative ones. A payload never repeats them (plan DF9/DF36).
/// </para>
/// <para>
///     This is not a stylistic split. Lookups, the conditional consumption of an authorization code (DF11) and
///     every cleanup predicate (DF17) evaluate the columns; if the payload carried its own copy, a divergence
///     would let the database validate one client, redirect URI or expiration while the materialized object
///     carried another. With a single source there is nothing to diverge.
/// </para>
/// <para>
///     Columns that are pure projections of payload content — <c>subject_id</c> and, for tokens,
///     <c>session_id</c>, both derived from the claims — are the exception: they exist for indexing and
///     subject-scoped removal, are written from the same source as the payload, and are never read back into
///     the model.
/// </para>
/// </summary>
/// <param name="Jti">
/// The lookup argument. It rematerializes <see cref="AccessToken.Id"/> and, for a reference token whose bearer
/// coincides with it, <see cref="TokenBase.Token"/> (plan DF13/DF38).
/// </param>
/// <param name="RealmId">The owning realm.</param>
/// <param name="ClientId">The client the token was issued to.</param>
/// <param name="AccessTokenType">Reference or JWT.</param>
/// <param name="CreatedAtUtc">Issuance instant.</param>
/// <param name="ExpiresAtUtc">Absolute expiration; with <paramref name="CreatedAtUtc"/> it yields the lifetime.</param>
public sealed record AccessTokenIdentity(
    string Jti,
    string RealmId,
    string ClientId,
    AccessTokenType AccessTokenType,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc)
{
    /// <summary>
    /// Lifetime in seconds, derived from the two authoritative timestamps rather than persisted a second time.
    /// An expiration that precedes the creation instant is incoherent data and fails closed.
    /// </summary>
    public int Lifetime => ExpiresAtUtc >= CreatedAtUtc
        ? (int)(ExpiresAtUtc - CreatedAtUtc).TotalSeconds
        : throw OperationalPayloadException.IncoherentRecord(
            nameof(AccessToken), "the expiration precedes the creation instant");
}

/// <inheritdoc cref="AccessTokenIdentity"/>
/// <param name="Handle">The lookup argument; it rematerializes <see cref="TokenBase.Token"/>.</param>
/// <param name="RealmId">The owning realm.</param>
/// <param name="ClientId">The client the token was issued to.</param>
/// <param name="CreatedAtUtc">Issuance instant.</param>
/// <param name="ExpiresAtUtc">Absolute expiration.</param>
public sealed record RefreshTokenIdentity(
    string Handle,
    string RealmId,
    string ClientId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc)
{
    /// <inheritdoc cref="AccessTokenIdentity.Lifetime"/>
    public int Lifetime => ExpiresAtUtc >= CreatedAtUtc
        ? (int)(ExpiresAtUtc - CreatedAtUtc).TotalSeconds
        : throw OperationalPayloadException.IncoherentRecord(
            nameof(RefreshToken), "the expiration precedes the creation instant");
}

/// <inheritdoc cref="AccessTokenIdentity"/>
/// <param name="Code">The lookup argument; it rematerializes <see cref="AuthorizationCode.Code"/>.</param>
/// <param name="RealmId">The owning realm.</param>
/// <param name="ClientId">The client the code was issued to — part of the consumption condition (DF11).</param>
/// <param name="RedirectUri">The redirect URI the code was issued for — also part of that condition.</param>
/// <param name="SessionId">The SSO session, when the code has one.</param>
/// <param name="CreatedAtUtc">Issuance instant.</param>
/// <param name="ExpiresAtUtc">Absolute expiration.</param>
public sealed record AuthorizationCodeIdentity(
    string Code,
    string RealmId,
    string ClientId,
    string RedirectUri,
    string? SessionId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc)
{
    /// <inheritdoc cref="AccessTokenIdentity.Lifetime"/>
    public int Lifetime => ExpiresAtUtc >= CreatedAtUtc
        ? (int)(ExpiresAtUtc - CreatedAtUtc).TotalSeconds
        : throw OperationalPayloadException.IncoherentRecord(
            nameof(AuthorizationCode), "the expiration precedes the creation instant");
}
