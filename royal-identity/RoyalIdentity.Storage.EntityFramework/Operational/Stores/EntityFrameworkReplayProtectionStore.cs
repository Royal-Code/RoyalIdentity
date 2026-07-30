using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Durable replay protection over <c>replay_handles</c> (matrix RC-01/RC-02), shared by every instance reading
/// the same Operational database.
/// <para>
/// The whole operation is one insert. There is no read before it — a read would let two concurrent callers both
/// find nothing — and no upsert: the primary key is the decision, and violating it is the answer. Expiration is
/// never part of the comparison, so correctness depends on neither the clock nor whether cleanup has run
/// (plan-replay-protection DF8).
/// </para>
/// <para>
/// Unlike the other stores of this family it is not realm-bound at construction: replay protection is asked
/// about one realm per call and never enumerates, so the realm is an argument rather than a fixture.
/// </para>
/// <para>
/// It saves through the scoped context shared with the other stores, exactly as they do — each of them adds and
/// saves in one step, so nothing accumulates between them. Client authentication also runs before any handler
/// writes, so there is nothing pending for this save to flush early.
/// </para>
/// </summary>
internal sealed class EntityFrameworkReplayProtectionStore(
    IOperationalDbContextAccessor accessor,
    ReplayHandleDigest digest) : IReplayProtectionStore
{
    public async Task<bool> TryAddAsync(
        string realmId,
        string issuer,
        string purpose,
        string handle,
        DateTimeOffset expiration,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        var handleDigest = digest.Compute(purpose, handle);
        var row = new ReplayHandleEntity
        {
            RealmId = realmId,
            Issuer = issuer,
            Purpose = purpose,
            HandleDigest = handleDigest,
            ExpiresAtUtc = expiration.UtcDateTime,
        };

        var db = accessor.DbContext;
        db.Add(row);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // The insert failed. Only one thing about this table can make that mean "replay": the key already
            // being taken. Everything else — a timeout, a dropped connection, a broken schema — must reach the
            // caller, because reporting a database outage as a replay attack is both a lie and an alarm nobody
            // can act on.
            //
            // The question is answered by asking it directly rather than by matching a provider's error code:
            // "is this identity now present?". That needs no provider reference in this neutral adapter, and it
            // is the actual question — a code only ever approximates it. It runs on the failure path alone, and
            // it does not read the expiration of what it finds (DF8).
            // If the probe itself fails, its exception propagates instead of this one. That is deliberate: both
            // say the database is unusable, and the more recent failure is the one an operator is looking at.
            if (await IsPresentAsync(realmId, issuer, purpose, handleDigest, ct))
                return false;

            throw;
        }
        finally
        {
            // A failed SaveChanges leaves the entry Added; left tracked, the next SaveChanges on this shared
            // context would try to insert it again.
            db.Entry(row).State = EntityState.Detached;
        }
    }

    private Task<bool> IsPresentAsync(
        string realmId, string issuer, string purpose, string handleDigest, CancellationToken ct)
        => accessor.DbContext.Set<ReplayHandleEntity>()
            .AsNoTracking()
            .Where(record => record.RealmId == realmId)
            .Where(record => record.Issuer == issuer)
            .Where(record => record.Purpose == purpose)
            .AnyAsync(record => record.HandleDigest == handleDigest, ct);
}
