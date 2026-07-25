using RoyalIdentity.Storage.EntityFramework.Migrations;

namespace Tests.Storage.Operational;

/// <summary>
/// Pins the migrations history topology of plan-data-operational-storage DF23 without opening a connection.
/// Configuration and Operational may share one database, so the four histories must be distinguishable: on
/// PostgreSQL by schema, on SQLite — which has none — by name. The runner, the design-time factories and the
/// fixtures all read this single source, so they cannot drift apart; the phases that build options for
/// migrations (Fase 2 for SQLite, Fase 7 for PostgreSQL) consume exactly these values.
/// </summary>
public class OperationalModelMigrationsHistoryTests
{
    [Fact]
    public void Sqlite_UsesDistinctHistoryNames_WithoutSchema()
    {
        var configuration = StorageMigrationsHistory.For(StorageFamily.Configuration, StorageProviderKind.Sqlite);
        var operational = StorageMigrationsHistory.For(StorageFamily.Operational, StorageProviderKind.Sqlite);

        Assert.Equal("__ConfigurationMigrationsHistory", configuration.Name);
        Assert.Equal("__OperationalMigrationsHistory", operational.Name);
        Assert.Null(configuration.Schema);
        Assert.Null(operational.Schema);
        Assert.NotEqual(configuration.Name, operational.Name);
    }

    [Fact]
    public void PostgreSql_KeepsTheDefaultName_IsolatedBySchema()
    {
        var configuration = StorageMigrationsHistory.For(StorageFamily.Configuration, StorageProviderKind.PostgreSql);
        var operational = StorageMigrationsHistory.For(StorageFamily.Operational, StorageProviderKind.PostgreSql);

        Assert.Equal("__EFMigrationsHistory", configuration.Name);
        Assert.Equal("__EFMigrationsHistory", operational.Name);
        Assert.Equal("configuration", configuration.Schema);
        Assert.Equal("operation", operational.Schema);
    }

    // The two families must never resolve to the same (name, schema) pair on any provider — that is exactly
    // what would mix their evolution lines in a shared database.
    [Theory]
    [InlineData(StorageProviderKind.Sqlite)]
    [InlineData(StorageProviderKind.PostgreSql)]
    public void TheTwoFamilies_NeverResolveToTheSameHistoryTable(StorageProviderKind provider)
    {
        var configuration = StorageMigrationsHistory.For(StorageFamily.Configuration, provider);
        var operational = StorageMigrationsHistory.For(StorageFamily.Operational, provider);

        Assert.NotEqual(configuration, operational);
    }

    // The legacy history is the starting point of the runner bootstrap, not a target of either family.
    [Fact]
    public void LegacyHistory_IsTheDefaultNameWithoutSchema_AndIsNoFamilysTarget()
    {
        Assert.Equal("__EFMigrationsHistory", StorageMigrationsHistory.Legacy.Name);
        Assert.Null(StorageMigrationsHistory.Legacy.Schema);

        foreach (var family in Enum.GetValues<StorageFamily>())
        {
            foreach (var provider in Enum.GetValues<StorageProviderKind>())
                Assert.NotEqual(StorageMigrationsHistory.Legacy, StorageMigrationsHistory.For(family, provider));
        }
    }
}
