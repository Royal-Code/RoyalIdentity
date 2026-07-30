using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// The PostgreSQL counterpart of <see cref="SqliteOperationalFileDatabase"/>: an isolated database whose
/// writers get genuinely independent connections, scopes and <c>DbContext</c>s. Pooling is off so each scope
/// really opens its own connection, which is what makes a concurrency acceptance an assertion about the store
/// rather than about a shared connection.
/// <para>
/// This matters more on PostgreSQL than anywhere else: MP-2 and MP-3 are implemented with provider-neutral
/// EF primitives (affected-row counts on a conditional update/delete), so proving they still produce exactly
/// one winner under a real MVCC engine — with different locking and isolation from SQLite's — is the point of
/// the phase.
/// </para>
/// </summary>
internal sealed class PostgreSqlOperationalConcurrencyDatabase : IAsyncDisposable
{
    private readonly string administrativeConnectionString;
    private readonly string databaseName;
    private readonly ServiceProvider services;

    private PostgreSqlOperationalConcurrencyDatabase(
        string administrativeConnectionString,
        string databaseName,
        string connectionString,
        ServiceProvider services)
    {
        this.administrativeConnectionString = administrativeConnectionString;
        this.databaseName = databaseName;
        this.services = services;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgreSqlOperationalConcurrencyDatabase> CreateMigratedAsync()
    {
        var administrativeConnectionString = Configuration.StoragePostgreSqlTestEnvironment.ConnectionString;
        var databaseName = $"royalidentity_concurrency_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(administrativeConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(administrativeConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        };
        var connectionString = builder.ConnectionString;

        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContext<OperationalPostgreSqlDbContext>(
            options => options.UseNpgsql(connectionString, npgsql => npgsql.UseOperationalMigrationsHistory()),
            ServiceLifetime.Scoped);
        collection.AddEntityFrameworkOperationalStorage<OperationalPostgreSqlDbContext>();
        collection.AddOperationalReplayProtection();
        collection.AddOperationalAesGcmPayloadProtection(
            OperationalStorageOptions.DefaultPayloadProtectionProfile, [.. ProtectorKey]);

        var services = collection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var database = new PostgreSqlOperationalConcurrencyDatabase(
            administrativeConnectionString, databaseName, connectionString, services);

        try
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<OperationalPostgreSqlDbContext>();
            await context.Database.MigrateAsync();

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>An independent scope: its own <c>DbContext</c> and, with pooling off, its own connection.</summary>
    public AsyncServiceScope CreateScope() => services.CreateAsyncScope();

    /// <summary>The durable replay-protection backing of this scope, over this scope's own connection.</summary>
    public IReplayProtectionStore ReplayProtectionOf(AsyncServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IReplayProtectionStore>();

    public IOperationalStoreFactory StoresOf(AsyncServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IOperationalStoreFactory>();

    /// <summary>Counts rows over its own connection, so the assertion never reads a change tracker.</summary>
    public async Task<int> CountAsync(string table)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM operation.{table}", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static Realm NewRealm(string id = "concurrency-realm")
    {
        var realm = new Realm(id, $"{id}.test", id, id, false, new RealmOptions(new ServerOptions()));
        realm.Options.OperationalStorage.PayloadProtectionProfile =
            OperationalStorageOptions.DefaultPayloadProtectionProfile;

        return realm;
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(administrativeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static ReadOnlySpan<byte> ProtectorKey
        =>
        [
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
            0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
        ];
}
