using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Realm-bound authorization-code store over <c>protocol_artifacts</c> (matrix AC-01..AC-03) plus the
/// single-use consumption of MP-2.
/// <para>
/// The code is located by <c>SHA-256</c> of its handle; the raw value is never persisted and comes back from
/// the lookup argument (plan DF38). Client and redirect URI are queryable columns precisely because the
/// consumption condition evaluates them in the database (plan DF11/DF45).
/// </para>
/// </summary>
internal sealed class EntityFrameworkAuthorizationCodeStore(
    Realm realm,
    IOperationalDbContextAccessor accessor,
    OperationalLookupDigest digest,
    AuthorizationCodePayloadSerializer serializer,
    OperationalPayloadProtection protection) : IOperationalAuthorizationCodeStore
{
    public async Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(code);

        var lookupDigest = digest.Compute(OperationalRecordTypes.AuthorizationCode, code.Code);
        var (payloadVersion, json) = serializer.Serialize(code);
        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.AuthorizationCode, lookupDigest, payloadVersion);

        var row = new ProtocolArtifactEntity
        {
            RealmId = realm.Id,
            ArtifactType = ProtocolArtifactTypes.AuthorizationCode,
            LookupDigest = lookupDigest,
            SubjectId = code.Subject.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            ClientId = code.ClientId,
            SessionId = code.SessionId,
            RedirectUri = code.RedirectUri,
            CreatedAtUtc = code.CreationTime,
            ExpiresAtUtc = code.CreationTime.AddSeconds(code.Lifetime),
            PayloadVersion = payloadVersion,
            ProtectedPayload = await protection.ProtectAsync(realm, json, context, ct),
        };

        var db = accessor.DbContext;
        db.Add(row);
        try
        {
            // AC-01 is create-only: the primary key is the authority, so a duplicate handle in the same realm
            // fails visibly instead of overwriting a live code.
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.Entry(row).State = EntityState.Detached;
        }

        return code.Code;
    }

    public async Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        var lookupDigest = digest.Compute(OperationalRecordTypes.AuthorizationCode, code);
        var row = await Artifacts()
            .SingleOrDefaultAsync(artifact => artifact.LookupDigest == lookupDigest, ct);

        // AC-02: the read does not filter expiration — it is the pipeline that owns that rule.
        return row is null ? null : await MaterializeAsync(row, code, ct);
    }

    public async Task RemoveAuthorizationCodeAsync(string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        var lookupDigest = digest.Compute(OperationalRecordTypes.AuthorizationCode, code);

        // AC-03 stays the administrative removal, and it stays idempotent: removing an absent code affects no
        // row and is not an error.
        await Artifacts()
            .Where(artifact => artifact.LookupDigest == lookupDigest)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<AuthorizationCode?> ConsumeAuthorizationCodeAsync(
        string code, string clientId, string redirectUri, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(redirectUri);

        var lookupDigest = digest.Compute(OperationalRecordTypes.AuthorizationCode, code);

        // The expected binding is part of the condition, so a mismatched client or redirect URI matches no row
        // and therefore consumes nothing (plan DF11).
        var bound = Artifacts()
            .Where(artifact => artifact.LookupDigest == lookupDigest)
            .Where(artifact => artifact.ClientId == clientId)
            .Where(artifact => artifact.RedirectUri == redirectUri);

        var row = await bound.SingleOrDefaultAsync(ct);
        if (row is null)
            return null;

        // The delete is the decision point: under concurrency exactly one caller affects the row, and only that
        // caller gets the code. A caller that read the same row but lost the delete sees zero and returns null,
        // indistinguishable from an absent code or a mismatched binding.
        if (await bound.ExecuteDeleteAsync(ct) is 0)
            return null;

        // Expiration is deliberately not part of the condition: an expired code is still consumed, and the
        // pipeline rejects it afterwards — so a losing attempt can never retry it (plan DF11).
        return await MaterializeAsync(row, code, ct);
    }

    private async Task<AuthorizationCode> MaterializeAsync(
        ProtocolArtifactEntity row, string code, CancellationToken ct)
    {
        if (row.PayloadVersion is null || row.ProtectedPayload is null)
        {
            throw OperationalPayloadException.IncoherentRecord(
                nameof(AuthorizationCode), "the persisted code has no payload");
        }

        if (row.RedirectUri is null)
        {
            throw OperationalPayloadException.IncoherentRecord(
                nameof(AuthorizationCode), "the persisted code has no redirect URI");
        }

        var lookupDigest = row.LookupDigest;
        var context = new OperationalProtectionContext(
            realm.Id, OperationalRecordTypes.AuthorizationCode, lookupDigest, row.PayloadVersion.Value);
        var json = await protection.UnprotectAsync(row.ProtectedPayload, context, ct);
        var identity = new AuthorizationCodeIdentity(
            code,
            row.RealmId,
            row.ClientId,
            row.RedirectUri,
            row.SessionId,
            row.CreatedAtUtc,
            row.ExpiresAtUtc);

        return serializer.Deserialize(row.PayloadVersion.Value, json, identity);
    }

    /// <summary>
    /// The realm's authorization codes. <c>artifact_type</c> is part of every query, so this store can never
    /// see an access or refresh token sharing the table (plan DF36).
    /// </summary>
    private IQueryable<ProtocolArtifactEntity> Artifacts()
        => accessor.DbContext.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact => artifact.RealmId == realm.Id)
            .Where(artifact => artifact.ArtifactType == ProtocolArtifactTypes.AuthorizationCode);
}
