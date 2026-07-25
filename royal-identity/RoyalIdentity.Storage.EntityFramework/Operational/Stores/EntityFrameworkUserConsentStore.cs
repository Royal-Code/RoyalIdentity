using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Realm-bound user consent store over <c>consents</c> (matrix CN-01..CN-03).
/// <para>
/// The identity is the real composite key <c>(realm, subject, client)</c> — never a concatenated string, which
/// in the transitional fake could collide on a subject or client containing the separator. Writes are an upsert
/// whose outcome is indivisible per key: concurrent writers cannot produce two rows, and the last write to
/// complete is the effective one (plan DF7/DF14).
/// </para>
/// </summary>
internal sealed class EntityFrameworkUserConsentStore(
    Realm realm,
    IOperationalDbContextAccessor accessor,
    ConsentPayloadSerializer serializer,
    OperationalPayloadProtection protection) : IUserConsentStore
{
    public async Task StoreUserConsentAsync(Consent consent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consent);

        var (payloadVersion, json) = serializer.Serialize(consent);
        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.Consent, LookupKey(consent.SubjectId, consent.ClientId), payloadVersion);
        var protectedPayload = await protection.ProtectAsync(realm, json, context, ct);

        // Update first: the common case is re-consenting, and a set-based update is indivisible per key.
        if (await UpdateAsync(consent, payloadVersion, protectedPayload, ct) is not 0)
            return;

        var db = accessor.DbContext;
        var row = new ConsentEntity
        {
            RealmId = realm.Id,
            SubjectId = consent.SubjectId,
            ClientId = consent.ClientId,
            CreatedAtUtc = consent.CreationTime,
            ExpiresAtUtc = consent.Expiration,
            PayloadVersion = payloadVersion,
            ProtectedPayload = protectedPayload,
        };

        db.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent writer inserted the same key between the update and this insert. The key constraint
            // is what prevents the duplicate; this caller simply becomes the later of the two writes.
            db.Entry(row).State = EntityState.Detached;

            if (await UpdateAsync(consent, payloadVersion, protectedPayload, ct) is 0)
                throw;

            return;
        }

        db.Entry(row).State = EntityState.Detached;
    }

    public async Task<Consent?> GetUserConsentAsync(string subjectId, string clientId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        var row = await Consents()
            .SingleOrDefaultAsync(
                consent => consent.SubjectId == subjectId && consent.ClientId == clientId, ct);

        // CN-02: the read never filters the expiration — the consent service owns that rule.
        if (row is null)
            return null;

        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.Consent, LookupKey(subjectId, clientId), row.PayloadVersion);
        var json = await protection.UnprotectAsync(row.ProtectedPayload, context, ct);

        return serializer.Deserialize(
            row.PayloadVersion,
            json,
            row.RealmId,
            row.SubjectId,
            row.ClientId,
            row.CreatedAtUtc,
            row.ExpiresAtUtc);
    }

    public async Task RemoveUserConsentAsync(string subjectId, string clientId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        // CN-03 is idempotent: removing an absent consent affects no row and is not an error.
        await Consents()
            .Where(consent => consent.SubjectId == subjectId && consent.ClientId == clientId)
            .ExecuteDeleteAsync(ct);
    }

    private IQueryable<ConsentEntity> Consents()
        => accessor.DbContext.Set<ConsentEntity>()
            .AsNoTracking()
            .Where(consent => consent.RealmId == realm.Id);

    private Task<int> UpdateAsync(
        Consent consent, int payloadVersion, string protectedPayload, CancellationToken ct)
        => Consents()
            .Where(row => row.SubjectId == consent.SubjectId && row.ClientId == consent.ClientId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.CreatedAtUtc, consent.CreationTime)
                    .SetProperty(row => row.ExpiresAtUtc, consent.Expiration)
                    .SetProperty(row => row.PayloadVersion, payloadVersion)
                    .SetProperty(row => row.ProtectedPayload, protectedPayload),
                ct);

    /// <summary>
    /// The lookup key bound into the protected payload's authenticated context. A consent has no handle to
    /// digest, so its identity is the composite key itself, length-prefixed so no subject/client pair can
    /// produce the encoding of another (plan DF30).
    /// </summary>
    private static string LookupKey(string subjectId, string clientId)
        => $"{subjectId.Length}:{subjectId}|{clientId.Length}:{clientId}";
}
