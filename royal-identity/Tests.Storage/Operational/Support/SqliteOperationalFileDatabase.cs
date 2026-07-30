using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Extensions;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// A file-backed SQLite database whose writers get genuinely independent connections, scopes and
/// <c>DbContext</c>s — the only shape in which a concurrency acceptance means anything (plan, section
/// "Segurança, concorrência e confiabilidade"). The shared in-memory fixture cannot serve this purpose:
/// everything there funnels through one connection, so contention never happens.
/// <para>
/// Pooling is off so each scope really opens its own connection, and a busy timeout lets a writer that meets
/// SQLite's database-level write lock wait instead of failing — which is what makes "both operations complete"
/// an assertion about the store rather than about SQLite's locking.
/// </para>
/// </summary>
internal sealed class SqliteOperationalFileDatabase : IAsyncDisposable
{
    private readonly string path;
    private readonly ServiceProvider services;

    private SqliteOperationalFileDatabase(string path, ServiceProvider services)
    {
        this.path = path;
        this.services = services;
    }

    public string ConnectionString => $"Data Source={path};Pooling=False;Default Timeout=30";

    /// <param name="clock">
    /// A controllable clock for scenarios that need to act at a precise point inside a store operation.
    /// Registered before the storage extension, whose <c>TryAdd</c> then leaves it in place.
    /// </param>
    /// <param name="interceptor">
    /// An EF interceptor, for scenarios that must pin an interleaving between two commands of the same
    /// operation instead of hoping the scheduler produces it.
    /// </param>
    public static async Task<SqliteOperationalFileDatabase> CreateMigratedAsync(
        TimeProvider? clock = null, IInterceptor? interceptor = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"royalidentity-operational-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False;Default Timeout=30";

        var collection = new ServiceCollection();
        collection.AddLogging();

        if (clock is not null)
            collection.AddSingleton(clock);

        collection.AddDbContext<OperationalSqliteDbContext>(
            options =>
            {
                options.UseSqlite(connectionString, sqlite => sqlite.UseOperationalMigrationsHistory());

                if (interceptor is not null)
                    options.AddInterceptors(interceptor);
            },
            ServiceLifetime.Scoped);
        collection.AddEntityFrameworkOperationalStorage<OperationalSqliteDbContext>();
        collection.AddOperationalReplayProtection();
        collection.AddOperationalAesGcmPayloadProtection(
            OperationalStorageOptions.DefaultPayloadProtectionProfile, [.. ProtectorKey]);

        var services = collection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var database = new SqliteOperationalFileDatabase(path, services);
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>();
            await context.Database.MigrateAsync();
        }

        // WAL lets one writer proceed while readers continue, instead of the rollback journal's
        // reader-blocks-writer escalation, whose deadlocks SQLite reports immediately rather than retrying.
        // Without it, "concurrent writers" would measure SQLite's locking mode, not the store's upsert.
        await using (var connection = new SqliteConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await command.ExecuteNonQueryAsync();
        }

        return database;
    }

    /// <summary>
    /// An async start barrier. A blocking <see cref="Barrier"/> would park thread-pool threads inside async
    /// writers and can starve their own continuations; this releases everyone only after all of them are
    /// parked, without blocking a single thread.
    /// </summary>
    public static async Task RunTogetherAsync(int writers, Func<int, TaskCompletionSource, Task, Task> writer)
    {
        var ready = Enumerable.Range(0, writers)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = Enumerable.Range(0, writers)
            .Select(index => writer(index, ready[index], release.Task))
            .ToArray();

        await Task.WhenAll(ready.Select(signal => signal.Task)).WaitAsync(TimeSpan.FromSeconds(60));
        release.SetResult();

        await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(60));
    }

    /// <summary>An independent scope: its own <c>DbContext</c> and, with pooling off, its own connection.</summary>
    public AsyncServiceScope CreateScope() => services.CreateAsyncScope();

    public IOperationalStoreFactory StoresOf(AsyncServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IOperationalStoreFactory>();

    /// <summary>The durable replay-protection backing of this scope, over this scope's own connection.</summary>
    public IReplayProtectionStore ReplayProtectionOf(AsyncServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IReplayProtectionStore>();

    /// <summary>Removes a session out-of-band, standing in for cleanup or a realm purge.</summary>
    public async Task DeleteSessionAsync(string sessionId)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM user_sessions WHERE session_id = $id;";
        command.Parameters.AddWithValue("$id", sessionId);
        await command.ExecuteNonQueryAsync();
    }

    public Task<int> CountConsentsAsync() => CountAsync("consents");

    public Task<int> CountSessionClientsAsync() => CountAsync("user_session_clients");

    /// <summary>Counts rows over its own connection, so the assertion never reads a change tracker.</summary>
    public async Task<int> CountAsync(string table)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";

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
        SqliteConnection.ClearAllPools();

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A file left behind in the temp directory is not worth failing a test over.
            }
        }
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
