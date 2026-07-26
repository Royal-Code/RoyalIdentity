using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoyalIdentity.Data.Operational.Entities;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

/// <summary>
/// EF implementation of the Operational maintenance (plan DF17/DF18). Every predicate below mirrors the
/// eligibility the plan fixes per lifecycle, and every one of them is set-based and bounded by a batch, so a
/// pass never holds the table for long and repeating it is harmless.
/// <para>
/// Nothing here logs a handle, a subject or a payload: telemetry is counts by type (plan DF28).
/// </para>
/// </summary>
internal sealed class EntityFrameworkOperationalMaintenance(
    IOperationalDbContextAccessor accessor,
    IOptions<OperationalCleanupOptions> options) : IOperationalMaintenance
{
    public async Task<OperationalCleanupReport> CleanupAsync(
        DateTime now, int batchSize, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        // Expired artifacts of every kind: an abandoned authorization code, an access token past its lifetime.
        // A consumed authorization code is already gone — the atomic consumption removed it (plan DF11).
        var accessTokens = await DeleteBatchAsync(
            Artifacts(ProtocolArtifactTypes.AccessToken).Where(artifact => artifact.ExpiresAtUtc <= now),
            batchSize, ct);

        var authorizationCodes = await DeleteBatchAsync(
            Artifacts(ProtocolArtifactTypes.AuthorizationCode).Where(artifact => artifact.ExpiresAtUtc <= now),
            batchSize, ct);

        var refreshTokens = await DeleteBatchAsync(RefreshTokensEligible(now), batchSize, ct);

        // A consent without an expiration is never eligible: only an explicit removal or a realm purge takes it.
        var consents = await DeleteBatchAsync(
            Set<ConsentEntity>().Where(consent => consent.ExpiresAtUtc != null && consent.ExpiresAtUtc <= now),
            batchSize, ct);

        // A session is eligible once it expired or reached its terminal state; its clients go with it through
        // the owning foreign key.
        var sessions = await DeleteBatchAsync(
            Set<UserSessionEntity>().Where(session =>
                (session.ExpiresAtUtc != null && session.ExpiresAtUtc <= now) || session.EndedAtUtc != null),
            batchSize, ct);

        var authorizeParameters = await DeleteBatchAsync(
            Set<AuthorizeParametersEntity>().Where(record => record.ExpiresAtUtc <= now),
            batchSize, ct);

        return new OperationalCleanupReport(
            accessTokens, refreshTokens, authorizationCodes, consents, sessions, authorizeParameters);
    }

    public async Task<OperationalPurgeReport> PurgeRealmAsync(string realmId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(realmId);

        // Scoped by realm on every table, so a colliding id in another realm is untouched. Configuration and
        // UserAccounts are not reachable from here at all — the realm tombstone survives by construction.
        var artifacts = await Set<ProtocolArtifactEntity>()
            .Where(artifact => artifact.RealmId == realmId)
            .ExecuteDeleteAsync(ct);

        var consents = await Set<ConsentEntity>()
            .Where(consent => consent.RealmId == realmId)
            .ExecuteDeleteAsync(ct);

        // Session clients are removed by the owning foreign key; deleting them explicitly first keeps the count
        // honest on providers whose cascade the model does not run in the database.
        await Set<UserSessionClientEntity>()
            .Where(client => client.RealmId == realmId)
            .ExecuteDeleteAsync(ct);

        var sessions = await Set<UserSessionEntity>()
            .Where(session => session.RealmId == realmId)
            .ExecuteDeleteAsync(ct);

        var authorizeParameters = await Set<AuthorizeParametersEntity>()
            .Where(record => record.RealmId == realmId)
            .ExecuteDeleteAsync(ct);

        return new OperationalPurgeReport(artifacts, consents, sessions, authorizeParameters);
    }

    /// <summary>
    /// A refresh token is eligible once it expires. A consumed one is additionally eligible after the largest
    /// tolerance this deployment may have configured — never before, or cleanup would delete a token some
    /// client would still accept (plan DF17). With no configured maximum, only expiration applies.
    /// </summary>
    private IQueryable<ProtocolArtifactEntity> RefreshTokensEligible(DateTime now)
    {
        var artifacts = Artifacts(ProtocolArtifactTypes.RefreshToken);
        var tolerance = options.Value.MaxRefreshTokenPostConsumedTolerance;

        if (tolerance is not { } window || window == TimeSpan.MaxValue)
            return artifacts.Where(artifact => artifact.ExpiresAtUtc <= now);

        var consumedBefore = now - window;

        return artifacts.Where(artifact =>
            artifact.ExpiresAtUtc <= now
            || (artifact.ConsumedAtUtc != null && artifact.ConsumedAtUtc <= consumedBefore));
    }

    /// <summary>
    /// Deletes at most <paramref name="batchSize"/> rows of the query. The ordering is by expiration so the
    /// oldest go first, which keeps repeated passes making progress instead of revisiting the same rows.
    /// </summary>
    private static async Task<int> DeleteBatchAsync<TEntity>(
        IQueryable<TEntity> eligible, int batchSize, CancellationToken ct)
        where TEntity : class
        => await eligible.Take(batchSize).ExecuteDeleteAsync(ct);

    private IQueryable<ProtocolArtifactEntity> Artifacts(string artifactType)
        => Set<ProtocolArtifactEntity>().Where(artifact => artifact.ArtifactType == artifactType);

    private IQueryable<TEntity> Set<TEntity>() where TEntity : class
        => accessor.DbContext.Set<TEntity>().AsNoTracking();
}
