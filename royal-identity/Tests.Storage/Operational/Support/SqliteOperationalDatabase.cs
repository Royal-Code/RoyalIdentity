using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// A real SQLite database holding <b>both</b> families over a single shared in-memory connection, created by the
/// checked-in migrations — never <c>EnsureCreated</c>. Sharing one file is the interesting case: it is what
/// forces Configuration and Operational to keep separate migrations histories (plan DF23), and the fixture
/// applies each with its own configured history so a drift would surface here.
/// <para>
/// The connection stays open for the lifetime of the helper so the <c>:memory:</c> database survives between
/// contexts; each new context is an independent unit of work over the same schema, which is what lets
/// round-trip scenarios prove materialized graphs are independent.
/// </para>
/// </summary>
internal sealed class SqliteOperationalDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private SqliteOperationalDatabase(SqliteConnection connection) => this.connection = connection;

    public SqliteConnection Connection => connection;

    public static async Task<SqliteOperationalDatabase> CreateMigratedAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var database = new SqliteOperationalDatabase(connection);

        await using (var configuration = database.NewConfigurationContext())
        {
            await configuration.Database.EnsureDeletedAsync();
            await configuration.Database.MigrateAsync();
        }

        await using (var operational = database.NewOperationalContext())
            await operational.Database.MigrateAsync();

        return database;
    }

    public ConfigurationSqliteDbContext NewConfigurationContext()
        => new(new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
            .UseSqlite(connection, sqlite => sqlite.UseConfigurationMigrationsHistory())
            .Options);

    public OperationalSqliteDbContext NewOperationalContext()
        => new(new DbContextOptionsBuilder<OperationalSqliteDbContext>()
            .UseSqlite(connection, sqlite => sqlite.UseOperationalMigrationsHistory())
            .Options);

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
