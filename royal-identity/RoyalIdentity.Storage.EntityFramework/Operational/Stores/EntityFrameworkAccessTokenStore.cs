using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Realm-bound access-token store over <c>protocol_artifacts</c> (matrix AT-01..AT-04).
/// <para>
/// Reference tokens are always persisted. A JWT follows the realm's
/// <see cref="JwtAccessTokenPersistenceMode"/> (plan DF13/DF31): <c>None</c> writes no row at all,
/// <c>Metadata</c> writes the queryable graph without the compact JWT, and <c>Full</c> also keeps the compact
/// JWT inside the protected payload. The effective policy is captured at write time, so changing the realm
/// option later never reinterprets rows that already exist.
/// </para>
/// <para>
/// Every row is located by <c>SHA-256(jti)</c>; the raw <c>jti</c> — which for a reference token is also the
/// bearer — has no column and never enters the payload, and <see cref="GetAsync"/> rematerializes
/// <see cref="AccessToken.Id"/> and <see cref="TokenBase.Token"/> from its own argument (plan DF13/DF38).
/// </para>
/// </summary>
internal sealed class EntityFrameworkAccessTokenStore(
    Realm realm,
    IOperationalDbContextAccessor accessor,
    OperationalLookupDigest digest,
    AccessTokenPayloadSerializer serializer,
    OperationalPayloadProtection protection) : IAccessTokenStore
{
    public async Task<string> StoreAsync(AccessToken token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);

        var mode = EffectiveMode(token);
        if (mode is null)
            return token.Id;

        // Only a JWT in Full mode persists its token string. A reference bearer is never written even if it
        // somehow diverged from the jti: it always comes back from the lookup argument (plan DF13).
        var persistCompactToken = token.AccessTokenType is AccessTokenType.Jwt
            && mode is JwtAccessTokenPersistenceMode.Full;

        var lookupDigest = digest.Compute(OperationalRecordTypes.AccessToken, token.Id);
        var (payloadVersion, json) = serializer.Serialize(token, persistCompactToken);
        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.AccessToken, lookupDigest, payloadVersion);

        var row = new ProtocolArtifactEntity
        {
            RealmId = realm.Id,
            ArtifactType = ProtocolArtifactTypes.AccessToken,
            LookupDigest = lookupDigest,
            SubjectId = token.SubjectId,
            ClientId = token.ClientId,
            SessionId = token.SessionId,
            AccessTokenType = (int)token.AccessTokenType,
            CreatedAtUtc = token.CreationTime,
            ExpiresAtUtc = token.CreationTime.AddSeconds(token.Lifetime),
            PayloadVersion = payloadVersion,
            ProtectedPayload = await protection.ProtectAsync(realm, json, context, ct),
        };

        var db = accessor.DbContext;
        db.Add(row);
        try
        {
            // AT-01 is create-only: the primary key is the authority, so a duplicate jti in the same realm
            // fails visibly instead of overwriting a live token.
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.Entry(row).State = EntityState.Detached;
        }

        return token.Id;
    }

    public async Task<AccessToken?> GetAsync(string jti, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(jti);

        var lookupDigest = digest.Compute(OperationalRecordTypes.AccessToken, jti);
        var row = await Artifacts()
            .SingleOrDefaultAsync(artifact => artifact.LookupDigest == lookupDigest, ct);

        // AT-02: expiration is data, not a filter — an expired token is returned until cleanup removes it.
        if (row is null || row.PayloadVersion is null || row.ProtectedPayload is null)
            return null;

        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.AccessToken, lookupDigest, row.PayloadVersion.Value);
        var json = await protection.UnprotectAsync(row.ProtectedPayload, context, ct);
        var identity = new AccessTokenIdentity(
            jti,
            row.RealmId,
            row.ClientId,
            (AccessTokenType)(row.AccessTokenType ?? (int)AccessTokenType.Jwt),
            row.CreatedAtUtc,
            row.ExpiresAtUtc);

        return serializer.Deserialize(row.PayloadVersion.Value, json, identity);
    }

    public async Task RemoveAsync(string jti, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(jti);

        var lookupDigest = digest.Compute(OperationalRecordTypes.AccessToken, jti);

        // AT-03 is idempotent: removing an absent token affects no row and is not an error.
        await Artifacts()
            .Where(artifact => artifact.LookupDigest == lookupDigest)
            .ExecuteDeleteAsync(ct);
    }

    public async Task RemoveReferenceTokensAsync(string subjectId, string clientId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        // AT-04: the filter is exact — reference tokens of this subject and client in this realm only. A JWT
        // artifact of the same subject/client is untouched, because persisting a JWT is not revocation
        // (plan DF13).
        await Artifacts()
            .Where(artifact => artifact.AccessTokenType == (int)AccessTokenType.Reference)
            .Where(artifact => artifact.SubjectId == subjectId)
            .Where(artifact => artifact.ClientId == clientId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// The realm's access-token artifacts. <c>artifact_type</c> is part of every query, so this store can never
    /// see a refresh token or an authorization code sharing the table (plan DF36).
    /// </summary>
    private IQueryable<ProtocolArtifactEntity> Artifacts()
        => accessor.DbContext.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact => artifact.RealmId == realm.Id)
            .Where(artifact => artifact.ArtifactType == ProtocolArtifactTypes.AccessToken);

    /// <summary>
    /// The persistence mode this write must obey, or <c>null</c> when nothing is written. Reference tokens
    /// ignore the realm option entirely — their persistence is what makes them verifiable.
    /// </summary>
    private JwtAccessTokenPersistenceMode? EffectiveMode(AccessToken token)
    {
        if (token.AccessTokenType is AccessTokenType.Reference)
            return JwtAccessTokenPersistenceMode.Full;

        var mode = realm.Options.OperationalStorage.JwtAccessTokenPersistence;

        return mode is JwtAccessTokenPersistenceMode.None ? null : mode;
    }
}
