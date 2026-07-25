using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Data.Operational.Entities;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// Public SQLite mapping extension for the Operational family (plan DF1/DF2): applies the neutral mappings
/// plus the SQLite refinements. Default and custom/combined contexts call this same extension; a model applies
/// exactly one provider extension per family.
/// </summary>
public static class SqliteOperationalModelBuilderExtensions
{
    public static ModelBuilder ApplyRoyalIdentityOperationalSqliteMappings(
        this ModelBuilder modelBuilder, OperationalModelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        options ??= new OperationalModelOptions();
        // DF4: SQLite uses the same table names without schema.
        options.Schema = null;

        modelBuilder.ApplyRoyalIdentityOperationalMappings(options);

        // Protected payloads are opaque TEXT on SQLite.
        modelBuilder.Entity<ProtocolArtifactEntity>().Property(e => e.ProtectedPayload).HasColumnType("TEXT");
        modelBuilder.Entity<ConsentEntity>().Property(e => e.ProtectedPayload).HasColumnType("TEXT");
        modelBuilder.Entity<AuthorizeParametersEntity>().Property(e => e.ProtectedPayload).HasColumnType("TEXT");

        // DF10: pin Ordinal (case-sensitive, byte-wise) comparison on every identifier that backs a key, an
        // index or an equality filter, so neither uniqueness nor a lookup ever depends on the provider's
        // default collation. SQLite's default happens to be BINARY, but declaring it makes the guarantee
        // explicit and testable.
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

        return modelBuilder;
    }

    /// <summary>SQLite's byte-wise (case-sensitive) collation, the Ordinal equivalent (plan DF10).</summary>
    private const string Ordinal = "BINARY";
}
