using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Integration.Prepare;

/// <summary>
/// Narrow test-only mutations for persisted Operational state that cannot be expressed as protocol behavior.
/// Scenarios identify records by their public opaque handle and never depend on relational entities.
/// </summary>
internal sealed class PersistentOperationalSetup(
    OperationalSqliteDbContext db,
    OperationalLookupDigest digest)
{
    public async Task SetRefreshTokenConsumedTimeAsync(
        string realmId,
        string refreshToken,
        DateTime consumedAtUtc,
        CancellationToken ct)
    {
        var lookupDigest = digest.Compute(OperationalRecordTypes.RefreshToken, refreshToken);
        var updated = await db.ProtocolArtifacts
            .Where(artifact =>
                artifact.RealmId == realmId
                && artifact.ArtifactType == ProtocolArtifactTypes.RefreshToken
                && artifact.LookupDigest == lookupDigest)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    artifact => artifact.ConsumedAtUtc,
                    consumedAtUtc),
                ct);

        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Refresh token setup expected one row in realm '{realmId}', but updated {updated}.");
        }
    }
}
