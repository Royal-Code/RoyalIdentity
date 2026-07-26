using Microsoft.EntityFrameworkCore;
using Npgsql;
using RoyalIdentity.Migrations;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

public class PostgreSqlConfigurationMigrationTests
{
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task Runner_Twice_CreatesEquivalentSchemaAndIdempotentProductSeed()
    {
        var options = new MigrationRunnerOptions
        {
            ConfigurationProvider = ConfigurationDatabaseProvider.PostgreSql,
            ConfigurationConnection = StoragePostgreSqlTestEnvironment.ConnectionString,
            Seed = ConfigurationSeedMode.Product,
            KeyProtector = ConfigurationKeyProtector.Plain,
            ProductSeed = new ConfigurationProductSeedOptions
            {
                ServerAdminRedirectUris = ["https://admin.example.test/callback"],
            },
        };

        await ConfigurationMigrationRunner.RunAsync(options);
        await ConfigurationMigrationRunner.RunAsync(options);

        await using var context = new ConfigurationPostgreSqlDbContext(
            new DbContextOptionsBuilder<ConfigurationPostgreSqlDbContext>()
                // DF23: the history lives in the `configuration` schema, so reading it needs the same
                // configuration the runner used — the entities' schema does not imply it.
                .UseNpgsql(
                    StoragePostgreSqlTestEnvironment.ConnectionString,
                    npgsql => npgsql.UseConfigurationMigrationsHistory())
                .Options);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, await context.Realms.CountAsync());
        Assert.Equal(1, await context.Clients.CountAsync(client => client.ClientId == "server_admin"));
        Assert.Equal(3, await context.SigningKeys.CountAsync());

        await using var connection = new NpgsqlConnection(StoragePostgreSqlTestEnvironment.ConnectionString);
        await connection.OpenAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(
            FindRepositoryRoot(),
            "scripts", "sql", "configuration", "postgresql", "0001_initial_configuration.sql"));
        await using (var scriptCommand = new NpgsqlCommand(script, connection) { CommandTimeout = 60 })
        {
            // The production script is idempotent: once migration history is present, repeated execution is a no-op.
            await scriptCommand.ExecuteNonQueryAsync();
            await scriptCommand.ExecuteNonQueryAsync();
        }

        Assert.Equal("jsonb", await ScalarAsync(connection,
            "SELECT data_type FROM information_schema.columns " +
            "WHERE table_schema = 'configuration' AND table_name = 'server_options' AND column_name = 'payload_json'"));
        Assert.Equal("C", await ScalarAsync(connection,
            "SELECT collation_name FROM information_schema.columns " +
            "WHERE table_schema = 'configuration' AND table_name = 'realms' AND column_name = 'domain'"));
        Assert.Equal("ux_realms_domain", await ScalarAsync(connection,
            "SELECT indexname FROM pg_indexes " +
            "WHERE schemaname = 'configuration' AND tablename = 'realms' AND indexname = 'ux_realms_domain'"));

        // DF23: the runner wrote the history into `configuration`, and nothing left one behind in `public`.
        Assert.Equal(1L, await CountAsync(connection,
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'configuration' AND table_name = '__EFMigrationsHistory'"));
        Assert.Equal(0L, await CountAsync(connection,
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'"));
    }

    // The versioned SQL is the reviewable path for operators who never run the runner. Applying the whole
    // sequence to an empty database must produce the same model — including the migration that drops a column,
    // which the scenario above can never reach because the runner had already applied everything.
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public async Task VersionedSqlSequence_OnAnEmptyDatabase_ProducesTheCurrentModel_AndIsIdempotent()
    {
        await using var database = await PostgreSqlConfigurationDatabase.CreateEmptyAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        var scripts = new[]
        {
            "0001_initial_configuration.sql",
            "0002_drop_update_access_token_claims_on_refresh.sql",
        };

        // Twice, in order: the second pass proves each script is a no-op once its migration is recorded.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var name in scripts)
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(
                    FindRepositoryRoot(), "scripts", "sql", "configuration", "postgresql", name));
                await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 60 };
                await command.ExecuteNonQueryAsync();
            }
        }

        // The obsolete column of Plano 2 is gone, and only the two known migrations are recorded.
        Assert.Equal(0L, await CountAsync(connection,
            "SELECT COUNT(*) FROM information_schema.columns " +
            "WHERE table_schema = 'configuration' AND table_name = 'clients' " +
            "AND column_name = 'update_access_token_claims_on_refresh'"));
        Assert.Equal(2L, await CountAsync(connection,
            "SELECT COUNT(*) FROM configuration.\"__EFMigrationsHistory\""));

        // And EF agrees the database is up to date, without the runner ever having touched it.
        await using var context = database.NewContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RoyalIdentity.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
