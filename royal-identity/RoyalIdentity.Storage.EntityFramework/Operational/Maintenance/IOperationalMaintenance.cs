namespace RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

/// <summary>How many rows each cleanup pass removed, by record type. Never the records themselves (plan DF17).</summary>
/// <param name="AccessTokens">Expired access-token artifacts.</param>
/// <param name="RefreshTokens">Expired refresh tokens, plus consumed ones past the configured tolerance.</param>
/// <param name="AuthorizationCodes">Abandoned codes that expired without being consumed.</param>
/// <param name="Consents">Consents that had an expiration and reached it.</param>
/// <param name="UserSessions">Sessions that expired or reached a terminal state.</param>
/// <param name="AuthorizeParameters">Authorize continuations that expired.</param>
public sealed record OperationalCleanupReport(
    int AccessTokens,
    int RefreshTokens,
    int AuthorizationCodes,
    int Consents,
    int UserSessions,
    int AuthorizeParameters)
{
    public static OperationalCleanupReport Empty { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>Total rows removed, for a caller that only needs to know whether the pass did anything.</summary>
    public int Total => AccessTokens + RefreshTokens + AuthorizationCodes + Consents + UserSessions + AuthorizeParameters;

    public OperationalCleanupReport Add(OperationalCleanupReport other) => new(
        AccessTokens + other.AccessTokens,
        RefreshTokens + other.RefreshTokens,
        AuthorizationCodes + other.AuthorizationCodes,
        Consents + other.Consents,
        UserSessions + other.UserSessions,
        AuthorizeParameters + other.AuthorizeParameters);
}

/// <summary>How many rows a realm purge removed, by table.</summary>
/// <param name="ProtocolArtifacts">Access tokens, refresh tokens and authorization codes.</param>
/// <param name="Consents">Consents.</param>
/// <param name="UserSessions">Sessions; their clients go with them through the owning foreign key.</param>
/// <param name="AuthorizeParameters">Authorize continuations.</param>
public sealed record OperationalPurgeReport(
    int ProtocolArtifacts,
    int Consents,
    int UserSessions,
    int AuthorizeParameters)
{
    public int Total => ProtocolArtifacts + Consents + UserSessions + AuthorizeParameters;
}

/// <summary>
/// <para>
///     Maintenance of the Operational data: physical cleanup by record type and the realm purge. It is a port of
///     this adapter, deliberately outside <c>IStorage</c> (plan DF17/DF18) — nothing in the request pipeline
///     should be able to delete data in bulk.
/// </para>
/// <para>
///     Cleanup is independent of any scheduler: the hosted worker and an external command/job call the very same
///     implementation, and exactly one of the two modes is enabled by the composition. It is also independent of
///     logical validity — an expired token is already invalid to a reader whether or not this ever runs.
/// </para>
/// </summary>
public interface IOperationalMaintenance
{
    /// <summary>
    /// Removes one batch of no-longer-observable records of every type. Idempotent, and safe to run
    /// concurrently: each pass only deletes rows that already satisfy its eligibility predicate.
    /// </summary>
    /// <param name="now">The instant eligibility is evaluated against, from the composition's clock.</param>
    /// <param name="batchSize">Maximum rows removed per type in this pass.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<OperationalCleanupReport> CleanupAsync(DateTime now, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Removes every Operational record of a realm. Idempotent, scoped to the realm — a colliding id in another
    /// realm is untouched — and it never reaches Configuration or UserAccounts: the realm tombstone and its
    /// path/domain reservation live there and must survive (plan DF18).
    /// </summary>
    Task<OperationalPurgeReport> PurgeRealmAsync(string realmId, CancellationToken ct = default);
}
