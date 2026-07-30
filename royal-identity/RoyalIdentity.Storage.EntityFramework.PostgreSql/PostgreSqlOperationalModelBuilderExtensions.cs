using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Data.Operational.Entities;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>
/// Public PostgreSQL mapping extension for the Operational family (plan DF1/DF2): applies the neutral mappings
/// plus the PostgreSQL refinements — the <c>operation</c> schema (plan DF4), opaque <c>text</c> payloads and
/// the byte-wise <c>C</c> collation. Default and custom/combined contexts call this same extension; a model
/// applies exactly one provider extension per family.
/// </summary>
public static class PostgreSqlOperationalModelBuilderExtensions
{
    public static ModelBuilder ApplyRoyalIdentityOperationalPostgreSqlMappings(
        this ModelBuilder modelBuilder, OperationalModelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        options ??= new OperationalModelOptions();
        // DF4: PostgreSQL isolates the family in its own schema, which is also what keeps the two migrations
        // histories apart when both families share one database (plan DF23).
        options.Schema ??= "operation";

        modelBuilder.ApplyRoyalIdentityOperationalMappings(options);

        // Protected payloads are opaque envelopes, never queried into. `text` says exactly that: `jsonb` is
        // reserved for the Configuration payloads, which are readable JSON documents (plan DF30).
        modelBuilder.Entity<ProtocolArtifactEntity>().Property(e => e.ProtectedPayload).HasColumnType("text");
        modelBuilder.Entity<ConsentEntity>().Property(e => e.ProtectedPayload).HasColumnType("text");
        modelBuilder.Entity<AuthorizeParametersEntity>().Property(e => e.ProtectedPayload).HasColumnType("text");

        // DF10: PostgreSQL's database default collation is deployment-defined, so every identifier that backs a
        // key, an index or an equality filter is pinned to the built-in byte-wise C collation. Without this,
        // uniqueness and lookups would depend on the database locale — the same handle could resolve, or fail
        // to resolve, depending on where the deployment happens to run.
        modelBuilder.Entity<ProtocolArtifactEntity>(entity =>
        {
            entity.Property(e => e.RealmId).UseCollation(Ordinal);
            entity.Property(e => e.ArtifactType).UseCollation(Ordinal);
            entity.Property(e => e.LookupDigest).UseCollation(Ordinal);
            entity.Property(e => e.SubjectId).UseCollation(Ordinal);
            entity.Property(e => e.ClientId).UseCollation(Ordinal);
            entity.Property(e => e.SessionId).UseCollation(Ordinal);
            entity.Property(e => e.RedirectUri).UseCollation(Ordinal);
        });

        modelBuilder.Entity<ConsentEntity>(entity =>
        {
            entity.Property(e => e.RealmId).UseCollation(Ordinal);
            entity.Property(e => e.SubjectId).UseCollation(Ordinal);
            entity.Property(e => e.ClientId).UseCollation(Ordinal);
        });

        modelBuilder.Entity<UserSessionEntity>(entity =>
        {
            entity.Property(e => e.RealmId).UseCollation(Ordinal);
            entity.Property(e => e.SessionId).UseCollation(Ordinal);
            entity.Property(e => e.SubjectId).UseCollation(Ordinal);
        });

        modelBuilder.Entity<UserSessionClientEntity>(entity =>
        {
            entity.Property(e => e.RealmId).UseCollation(Ordinal);
            entity.Property(e => e.SessionId).UseCollation(Ordinal);
            entity.Property(e => e.ClientId).UseCollation(Ordinal);
        });

        modelBuilder.Entity<AuthorizeParametersEntity>(entity =>
        {
            entity.Property(e => e.RealmId).UseCollation(Ordinal);
            entity.Property(e => e.HandleDigest).UseCollation(Ordinal);
        });

        // Every column of this table backs the key that decides replay, so every one of them is pinned. A
        // locale-dependent collation here would mean two handles differing only in case could collide, or fail
        // to collide, depending on where the deployment runs.
        modelBuilder.Entity<ReplayHandleEntity>(entity =>
        {
            entity.Property(e => e.RealmId).UseCollation(Ordinal);
            entity.Property(e => e.Issuer).UseCollation(Ordinal);
            entity.Property(e => e.Purpose).UseCollation(Ordinal);
            entity.Property(e => e.HandleDigest).UseCollation(Ordinal);
        });

        return modelBuilder;
    }

    /// <summary>PostgreSQL's built-in byte-wise collation, the Ordinal equivalent (plan DF10).</summary>
    private const string Ordinal = "C";
}
