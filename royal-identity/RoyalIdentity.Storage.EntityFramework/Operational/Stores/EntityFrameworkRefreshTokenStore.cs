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
/// Realm-bound refresh-token store over <c>protocol_artifacts</c> (matrix RT-01..RT-05) plus the conditional
/// transitions of MP-3.
/// <para>
/// Consumption state and <c>state_version</c> are queryable columns, so a transition is a conditional update
/// whose affected-row count decides the winner (plan DF12). The post-consumption tolerance is <b>not</b> part of
/// this primitive: a conflict is never turned into a success here, and it is the caller that decides what to do
/// with a rematerialized consumed token.
/// </para>
/// </summary>
internal sealed class EntityFrameworkRefreshTokenStore(
    Realm realm,
    IOperationalDbContextAccessor accessor,
    OperationalLookupDigest digest,
    RefreshTokenPayloadSerializer serializer,
    OperationalPayloadProtection protection) : IOperationalRefreshTokenStore
{
    public async Task StoreAsync(RefreshToken token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);

        var lookupDigest = Digest(token.Token);
        var row = new ProtocolArtifactEntity
        {
            RealmId = realm.Id,
            ArtifactType = ProtocolArtifactTypes.RefreshToken,
            LookupDigest = lookupDigest,
            SubjectId = token.SubjectId,
            ClientId = token.ClientId,
            SessionId = token.SessionId,
            CreatedAtUtc = token.CreationTime,
            ExpiresAtUtc = token.CreationTime.AddSeconds(token.Lifetime),
            ConsumedAtUtc = token.ConsumedTime,
            StateVersion = token.StateVersion,
            ClaimsMode = (int)token.ClaimsMode,
        };

        await WritePayloadAsync(row, token, lookupDigest, ct);

        var db = accessor.DbContext;
        db.Add(row);
        try
        {
            // RT-01 is create-only: the primary key is the authority, so a duplicate handle in the same realm
            // fails visibly instead of overwriting a live token.
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.Entry(row).State = EntityState.Detached;
        }
    }

    public async Task<RefreshToken?> GetAsync(string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var row = await FindAsync(token, ct);

        // RT-02: neither expiration nor consumption filters the read — the tolerance policy needs to see a
        // consumed token, and the expiration rule belongs to the pipeline.
        return row is null ? null : await MaterializeAsync(row, token, ct);
    }

    public async Task UpdateAsync(RefreshToken token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);

        // The unconditional update exists only for the legacy CRUD contract; in this adapter every write goes
        // through the conditional transition. Reporting the result matters: silently returning success on a
        // conflict would reintroduce exactly the lost update MP-3 exists to prevent, through the older API.
        var transition = await TryUpdateAsync(token, token.StateVersion, ct);

        if (!transition.IsSuccess)
        {
            throw new InvalidOperationException(
                $"The refresh token could not be updated: {transition.Outcome}. Use the conditional transition " +
                "(IVersionedRefreshTokenStore) to handle a concurrent writer explicitly.");
        }
    }

    public async Task RemoveAsync(string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var lookupDigest = Digest(token);

        // RT-04 is idempotent: removing an absent token affects no row and is not an error.
        await Artifacts()
            .Where(artifact => artifact.LookupDigest == lookupDigest)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> RemoveBySubjectAsync(string subjectId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);

        // RT-05: the count is of rows actually removed, so a repeat returns zero. Ordinal by collation, and
        // never across realms.
        return await Artifacts()
            .Where(artifact => artifact.SubjectId == subjectId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<RefreshTokenTransition> TryConsumeAsync(
        string token, int expectedStateVersion, DateTime consumedAt, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var lookupDigest = Digest(token);

        // The condition is "still unconsumed, at the version the caller materialized". Exactly one concurrent
        // caller can satisfy it, and the version bump makes any other writer's expectation stale.
        var affected = await Artifacts()
            .Where(artifact => artifact.LookupDigest == lookupDigest)
            .Where(artifact => artifact.ConsumedAtUtc == null)
            .Where(artifact => artifact.StateVersion == expectedStateVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(artifact => artifact.ConsumedAtUtc, consumedAt)
                    .SetProperty(artifact => artifact.StateVersion, artifact => artifact.StateVersion + 1),
                ct);

        var current = await FindAsync(token, ct);
        if (current is null)
            return RefreshTokenTransition.NotFound();

        var materialized = await MaterializeAsync(current, token, ct);

        if (affected is not 0)
            return RefreshTokenTransition.Succeeded(materialized);

        // Losing the condition is never a success. The caller gets the rematerialized state — the only thing it
        // may submit to the tolerance policy — and can tell "someone consumed it" from "someone else moved it".
        return current.ConsumedAtUtc is not null
            ? RefreshTokenTransition.AlreadyConsumed(materialized)
            : RefreshTokenTransition.Conflict(materialized);
    }

    public async Task<RefreshTokenTransition> TryUpdateAsync(
        RefreshToken token, int expectedStateVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);

        var lookupDigest = Digest(token.Token);
        var row = new ProtocolArtifactEntity
        {
            RealmId = realm.Id,
            ArtifactType = ProtocolArtifactTypes.RefreshToken,
            LookupDigest = lookupDigest,
            ClientId = token.ClientId,
        };
        await WritePayloadAsync(row, token, lookupDigest, ct);

        var payloadVersion = row.PayloadVersion!.Value;
        var protectedPayload = row.ProtectedPayload!;
        var nextVersion = expectedStateVersion + 1;

        var affected = await Artifacts()
            .Where(artifact => artifact.LookupDigest == lookupDigest)
            .Where(artifact => artifact.StateVersion == expectedStateVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(artifact => artifact.ConsumedAtUtc, token.ConsumedTime)
                    .SetProperty(artifact => artifact.ExpiresAtUtc, token.CreationTime.AddSeconds(token.Lifetime))
                    .SetProperty(artifact => artifact.ClaimsMode, (int)token.ClaimsMode)
                    .SetProperty(artifact => artifact.PayloadVersion, payloadVersion)
                    .SetProperty(artifact => artifact.ProtectedPayload, protectedPayload)
                    .SetProperty(artifact => artifact.StateVersion, nextVersion),
                ct);

        var current = await FindAsync(token.Token, ct);
        if (current is null)
            return RefreshTokenTransition.NotFound();

        var materialized = await MaterializeAsync(current, token.Token, ct);
        if (affected is 0)
            return RefreshTokenTransition.Conflict(materialized);

        // Keep the caller's instance in step with what was persisted, so a follow-up transition uses the new
        // version rather than the stale one it came in with.
        token.StateVersion = nextVersion;

        return RefreshTokenTransition.Succeeded(materialized);
    }

    private async Task WritePayloadAsync(
        ProtocolArtifactEntity row, RefreshToken token, string lookupDigest, CancellationToken ct)
    {
        var (payloadVersion, json) = serializer.Serialize(token);
        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.RefreshToken, lookupDigest, payloadVersion);

        row.PayloadVersion = payloadVersion;
        row.ProtectedPayload = await protection.ProtectAsync(realm, json, context, ct);
    }

    private async Task<RefreshToken> MaterializeAsync(
        ProtocolArtifactEntity row, string token, CancellationToken ct)
    {
        if (row.PayloadVersion is null || row.ProtectedPayload is null)
        {
            throw OperationalPayloadException.IncoherentRecord(
                nameof(RefreshToken), "the persisted refresh token has no payload");
        }

        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.RefreshToken, row.LookupDigest, row.PayloadVersion.Value);
        var json = await protection.UnprotectAsync(row.ProtectedPayload, context, ct);
        var identity = new RefreshTokenIdentity(
            token, row.RealmId, row.ClientId, row.CreatedAtUtc, row.ExpiresAtUtc);

        var materialized = serializer.Deserialize(row.PayloadVersion.Value, json, identity);

        // DF12: the version travels with the materialized token, because it is the expected value a later
        // conditional transition must present — never the state of the instance about to be written.
        materialized.StateVersion = row.StateVersion;
        materialized.ConsumedTime = row.ConsumedAtUtc;
        materialized.ClaimsMode = ClaimsModeOf(row);

        return materialized;
    }

    private static RefreshTokenClaimsMode ClaimsModeOf(ProtocolArtifactEntity row)
    {
        if (row.ClaimsMode is null || !Enum.IsDefined((RefreshTokenClaimsMode)row.ClaimsMode.Value))
        {
            throw OperationalPayloadException.IncoherentRecord(
                nameof(RefreshToken), "the persisted claims mode is missing or unknown");
        }

        return (RefreshTokenClaimsMode)row.ClaimsMode.Value;
    }

    private Task<ProtocolArtifactEntity?> FindAsync(string token, CancellationToken ct)
    {
        var lookupDigest = Digest(token);

        return Artifacts().SingleOrDefaultAsync(artifact => artifact.LookupDigest == lookupDigest, ct);
    }

    private string Digest(string token) => digest.Compute(OperationalRecordTypes.RefreshToken, token);

    /// <summary>
    /// The realm's refresh tokens. <c>artifact_type</c> is part of every query, so this store can never see an
    /// access token or an authorization code sharing the table (plan DF36).
    /// </summary>
    private IQueryable<ProtocolArtifactEntity> Artifacts()
        => accessor.DbContext.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact => artifact.RealmId == realm.Id)
            .Where(artifact => artifact.ArtifactType == ProtocolArtifactTypes.RefreshToken);
}
