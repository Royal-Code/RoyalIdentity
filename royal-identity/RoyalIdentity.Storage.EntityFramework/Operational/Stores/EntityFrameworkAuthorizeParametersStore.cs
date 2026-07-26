using System.Collections.Specialized;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Realm-bound authorize-parameters store over <c>authorize_parameters</c> (MP-5, matrix AP-01..AP-03).
/// <para>
/// This is the server-side continuation of an authorize request across the login and consent screens. Unlike
/// every other operational read, this one is <b>fail-closed</b>: an expired record reads as absent, because
/// resuming an abandoned authorize request is a risk rather than a feature (plan DF16). The expiration is
/// absolute and written at store time, so changing the realm's interaction lifetime never reinterprets records
/// that already exist.
/// </para>
/// </summary>
internal sealed class EntityFrameworkAuthorizeParametersStore(
    Realm realm,
    IOperationalDbContextAccessor accessor,
    IAuthorizeParametersHandleGenerator handleGenerator,
    OperationalLookupDigest digest,
    AuthorizeParametersPayloadSerializer serializer,
    OperationalPayloadProtection protection,
    TimeProvider clock) : IAuthorizeParametersStore
{
    /// <summary>How many times a colliding handle is regenerated before giving up.</summary>
    private const int CollisionRetries = 5;

    public async Task<string> WriteAsync(NameValueCollection parameters, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var (payloadVersion, json) = serializer.Serialize(parameters);
        var createdAt = clock.GetUtcNow().UtcDateTime;
        // DF16/DF40: absolute expiration computed now, in seconds, from the realm's interaction lifetime.
        var expiresAt = createdAt.AddSeconds(realm.Options.Authentication.AuthorizationInteractionLifetime);
        var db = accessor.DbContext;

        for (var attempt = 0; ; attempt++)
        {
            var handle = handleGenerator.Generate();
            var handleDigest = digest.Compute(OperationalRecordTypes.AuthorizeParameters, handle);
            var context = new OperationalProtectionContext(
                realm.Id, OperationalRecordTypes.AuthorizeParameters, handleDigest, payloadVersion);

            var row = new AuthorizeParametersEntity
            {
                RealmId = realm.Id,
                HandleDigest = handleDigest,
                CreatedAtUtc = createdAt,
                ExpiresAtUtc = expiresAt,
                PayloadVersion = payloadVersion,
                ProtectedPayload = await protection.ProtectAsync(realm, json, context, ct),
            };

            db.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
                return handle;
            }
            catch (DbUpdateException) when (attempt < CollisionRetries)
            {
                // AP-01: a collision is regenerated internally — it never overwrites the record it collided
                // with, and it never surfaces as a random failure to the caller. Exhausting the retries is a
                // different matter and propagates: at that point something other than luck is wrong.
            }
            finally
            {
                db.Entry(row).State = EntityState.Detached;
            }
        }
    }

    public async Task<NameValueCollection?> ReadAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var handleDigest = digest.Compute(OperationalRecordTypes.AuthorizeParameters, id);
        var row = await Records()
            .SingleOrDefaultAsync(record => record.HandleDigest == handleDigest, ct);

        if (row is null)
            return null;

        // AP-02 is fail-closed: past the window the record is absent, whether or not cleanup has run yet.
        if (row.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
        {
            await TryRemoveExpiredAsync(handleDigest, ct);
            return null;
        }

        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.AuthorizeParameters, handleDigest, row.PayloadVersion);
        var json = await protection.UnprotectAsync(row.ProtectedPayload, context, ct);

        // AP-02: the read does not consume — the flow reads the handle more than once before the callback
        // deletes it.
        return serializer.Deserialize(row.PayloadVersion, json);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var handleDigest = digest.Compute(OperationalRecordTypes.AuthorizeParameters, id);

        // AP-03 is idempotent: deleting an absent handle affects no row and is not an error.
        await Records()
            .Where(record => record.HandleDigest == handleDigest)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Lazy cleanup on read. It is opportunistic on purpose: a failure to remove the row must not turn into
    /// returning an expired payload, so the caller has already been told the record is absent (plan DF17).
    /// </summary>
    private async Task TryRemoveExpiredAsync(string handleDigest, CancellationToken ct)
    {
        try
        {
            await Records()
                .Where(record => record.HandleDigest == handleDigest)
                .ExecuteDeleteAsync(ct);
        }
        catch (DbException)
        {
            // Periodic cleanup will remove it; the read already failed closed.
        }
    }

    private IQueryable<AuthorizeParametersEntity> Records()
        => accessor.DbContext.Set<AuthorizeParametersEntity>()
            .AsNoTracking()
            .Where(record => record.RealmId == realm.Id);
}
