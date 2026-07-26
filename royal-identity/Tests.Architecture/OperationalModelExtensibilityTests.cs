using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Architecture;

/// <summary>
/// Proves the model extensibility decided in plan-data-operational-storage DF1/DF2: a custom context that
/// does NOT inherit from <see cref="OperationalDbContext"/> applies the public mapping extension and obtains
/// the Operational model — including a context that combines Configuration and Operational in one model, the
/// scenario the two families must support when they share a database. Building a model never touches a
/// database. Provider refinements for Operational land with each provider extension (Fase 2/Fase 7); this
/// phase proves the neutral mappings compose.
/// </summary>
public class OperationalModelExtensibilityTests
{
    private static readonly Type[] OperationalEntityTypes =
    [
        typeof(ProtocolArtifactEntity),
        typeof(ConsentEntity),
        typeof(UserSessionEntity),
        typeof(UserSessionClientEntity),
        typeof(AuthorizeParametersEntity),
    ];

    private static readonly Type[] ConfigurationEntityTypes =
    [
        typeof(ServerOptionsEntity),
        typeof(RealmEntity),
        typeof(ClientEntity),
        typeof(ClientStringValueEntity),
        typeof(ClientClaimEntity),
        typeof(ClientSecretEntity),
        typeof(SigningKeyEntity),
    ];

    // Combined-context stand-in (DF2): it inherits DbContext directly — never OperationalDbContext or
    // ConfigurationDbContext — and applies both families' public mapping extensions to a single model.
    private sealed class CombinedDbContext(DbContextOptions<CombinedDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyRoyalIdentityConfigurationSqliteMappings();
            modelBuilder.ApplyRoyalIdentityOperationalMappings(new OperationalModelOptions());
        }
    }

    // The PostgreSQL combined context (Fase 7): both families in one model, each applying its own provider
    // extension, which is what puts them in the `configuration` and `operation` schemas.
    private sealed class CombinedPostgreSqlDbContext(DbContextOptions<CombinedPostgreSqlDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyRoyalIdentityConfigurationPostgreSqlMappings();
            modelBuilder.ApplyRoyalIdentityOperationalPostgreSqlMappings();
        }
    }

    private static IModel BuildCombinedModel()
    {
        var options = new DbContextOptionsBuilder<CombinedDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new CombinedDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IModel BuildCombinedPostgreSqlModel()
    {
        var options = new DbContextOptionsBuilder<CombinedPostgreSqlDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;
        using var context = new CombinedPostgreSqlDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void DefaultOperationalDbContext_AppliesTheNeutralModel()
    {
        var options = new DbContextOptionsBuilder<OperationalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new OperationalDbContext(options);

        var mapped = context.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

        Assert.Equal(OperationalEntityTypes.ToHashSet(), mapped);
        Assert.Null(context.Model.FindEntityType(typeof(ProtocolArtifactEntity))!.GetSchema());
    }

    [Fact]
    public void CombinedContext_MapsBothFamilies_WithoutInheritingEitherDefaultContext()
    {
        var model = BuildCombinedModel();

        var mapped = model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

        Assert.Equal(ConfigurationEntityTypes.Concat(OperationalEntityTypes).ToHashSet(), mapped);
    }

    [Fact]
    public void CombinedContext_KeepsTheTwoFamiliesUnrelated()
    {
        var model = BuildCombinedModel();
        var configurationTypes = ConfigurationEntityTypes.ToHashSet();

        // DF6: even in one model over one database, no Operational entity may declare a relationship to a
        // Configuration entity — the two families may live in different databases.
        foreach (var entityType in OperationalEntityTypes)
        {
            var foreignKeys = model.FindEntityType(entityType)!.GetForeignKeys();

            Assert.DoesNotContain(foreignKeys, fk => configurationTypes.Contains(fk.PrincipalEntityType.ClrType));
        }
    }

    [Fact]
    public void CombinedContext_DoesNotCollideTableNamesBetweenFamilies()
    {
        var model = BuildCombinedModel();

        var tables = model.GetEntityTypes().Select(t => t.GetTableName()!).ToList();

        Assert.Equal(tables.Count, tables.Distinct(StringComparer.Ordinal).Count());
    }

    // Fase 7: the separate PostgreSQL Operational context applies its own extension and lands in `operation`.
    [Fact]
    public void OperationalPostgreSqlDbContext_MapsTheFamilyIntoTheOperationSchema()
    {
        var options = new DbContextOptionsBuilder<OperationalPostgreSqlDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;
        using var context = new OperationalPostgreSqlDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.Equal(OperationalEntityTypes.ToHashSet(), model.GetEntityTypes().Select(t => t.ClrType).ToHashSet());
        Assert.All(
            OperationalEntityTypes,
            entityType => Assert.Equal("operation", model.FindEntityType(entityType)!.GetSchema()));
    }

    // DF4/DF23: combined in one model, the two families still occupy their own schemas — which is exactly what
    // keeps their tables, and their migrations histories, from meeting.
    [Fact]
    public void CombinedPostgreSqlContext_KeepsEachFamilyInItsOwnSchema()
    {
        var model = BuildCombinedPostgreSqlModel();

        Assert.Equal(
            ConfigurationEntityTypes.Concat(OperationalEntityTypes).ToHashSet(),
            model.GetEntityTypes().Select(t => t.ClrType).ToHashSet());
        Assert.All(
            ConfigurationEntityTypes,
            entityType => Assert.Equal("configuration", model.FindEntityType(entityType)!.GetSchema()));
        Assert.All(
            OperationalEntityTypes,
            entityType => Assert.Equal("operation", model.FindEntityType(entityType)!.GetSchema()));

        // No default schema is declared, so nothing silently pulls a family — or the history table — elsewhere.
        Assert.Null(model.GetDefaultSchema());
    }

    [Fact]
    public void CombinedPostgreSqlContext_KeepsTheTwoFamiliesUnrelated()
    {
        var model = BuildCombinedPostgreSqlModel();
        var configurationTypes = ConfigurationEntityTypes.ToHashSet();

        foreach (var entityType in OperationalEntityTypes)
        {
            var foreignKeys = model.FindEntityType(entityType)!.GetForeignKeys();

            Assert.DoesNotContain(foreignKeys, fk => configurationTypes.Contains(fk.PrincipalEntityType.ClrType));
        }
    }
}
