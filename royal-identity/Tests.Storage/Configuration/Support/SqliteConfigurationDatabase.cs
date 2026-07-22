using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Storage.Configuration.Support;

/// <summary>
/// A real SQLite database created via the checked-in migration over a single shared in-memory connection
/// (plan Fase 2: no <c>EnsureCreated</c>). The connection is kept open for the lifetime of the helper so the
/// <c>:memory:</c> database survives between contexts; each <see cref="NewContext"/> is an independent unit of
/// work over the same schema, letting round-trip tests prove materialized graphs are independent.
/// </summary>
internal sealed class SqliteConfigurationDatabase : IAsyncDisposable
{
	private readonly SqliteConnection connection;

	private SqliteConfigurationDatabase(SqliteConnection connection) => this.connection = connection;

	public SqliteConnection Connection => connection;

	/// <summary>Opens the shared connection and applies the real migration (EnsureDeleted first, then MigrateAsync).</summary>
	public static async Task<SqliteConfigurationDatabase> CreateMigratedAsync()
	{
		var connection = new SqliteConnection("DataSource=:memory:");
		await connection.OpenAsync();

		var database = new SqliteConfigurationDatabase(connection);
		await using var context = database.NewContext();
		await context.Database.EnsureDeletedAsync();
		await context.Database.MigrateAsync();

		return database;
	}

	public ConfigurationSqliteDbContext NewContext()
	{
		var options = new DbContextOptionsBuilder<ConfigurationSqliteDbContext>()
			.UseSqlite(connection)
			.Options;

		return new ConfigurationSqliteDbContext(options);
	}

	public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
