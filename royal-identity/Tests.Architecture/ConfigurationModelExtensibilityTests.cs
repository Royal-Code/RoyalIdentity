using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Architecture;

/// <summary>
/// Proves the model extensibility decided in plan-data-configuration-storage DF3: a custom context that
/// does NOT inherit from <see cref="ConfigurationDbContext"/> applies the public mapping extension of
/// exactly one provider and obtains the neutral model plus that provider's refinements (schema/jsonb on
/// PostgreSQL; no schema/TEXT on SQLite). Building a model never touches a database.
/// </summary>
public class ConfigurationModelExtensibilityTests
{
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

	// Combined-context stand-ins (DF3): they inherit DbContext directly — never ConfigurationDbContext —
	// and would also apply the future Operational mappings of the same provider (Plano 3).
	private sealed class CombinedSqliteDbContext(DbContextOptions<CombinedSqliteDbContext> options) : DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ApplyRoyalIdentityConfigurationSqliteMappings();
		}
	}

	private sealed class CombinedPostgreSqlDbContext(DbContextOptions<CombinedPostgreSqlDbContext> options)
		: DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ApplyRoyalIdentityConfigurationPostgreSqlMappings();
		}
	}

	private static IModel BuildCombinedSqliteModel()
	{
		var options = new DbContextOptionsBuilder<CombinedSqliteDbContext>()
			.UseSqlite("Data Source=:memory:")
			.Options;
		using var context = new CombinedSqliteDbContext(options);
		return context.Model;
	}

	private static IModel BuildCombinedPostgreSqlModel()
	{
		var options = new DbContextOptionsBuilder<CombinedPostgreSqlDbContext>()
			.UseNpgsql("Host=model-only;Database=model-only")
			.Options;
		using var context = new CombinedPostgreSqlDbContext(options);
		return context.Model;
	}

	[Fact]
	public void CustomSqliteContext_GetsNeutralModel_WithSqliteRefinements()
	{
		var model = BuildCombinedSqliteModel();

		var mapped = model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();
		Assert.Equal(ConfigurationEntityTypes.ToHashSet(), mapped);

		var realms = model.FindEntityType(typeof(RealmEntity))!;
		Assert.Equal("realms", realms.GetTableName());
		Assert.Null(realms.GetSchema());

		var payload = model.FindEntityType(typeof(ServerOptionsEntity))!
			.FindProperty(nameof(ServerOptionsEntity.PayloadJson))!;
		Assert.Equal("TEXT", payload.GetColumnType());
	}

	[Fact]
	public void CustomPostgreSqlContext_GetsNeutralModel_WithSchemaAndJsonb()
	{
		var model = BuildCombinedPostgreSqlModel();

		var mapped = model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();
		Assert.Equal(ConfigurationEntityTypes.ToHashSet(), mapped);

		var realms = model.FindEntityType(typeof(RealmEntity))!;
		Assert.Equal("realms", realms.GetTableName());
		Assert.Equal("configuration", realms.GetSchema());

		var payload = model.FindEntityType(typeof(ServerOptionsEntity))!
			.FindProperty(nameof(ServerOptionsEntity.PayloadJson))!;
		Assert.Equal("jsonb", payload.GetColumnType());

		var optionsJson = model.FindEntityType(typeof(RealmEntity))!
			.FindProperty(nameof(RealmEntity.OptionsJson))!;
		Assert.Equal("jsonb", optionsJson.GetColumnType());
	}

	[Fact]
	public void BothProviderModels_ExposeTheSameTables()
	{
		var sqliteTables = BuildCombinedSqliteModel().GetEntityTypes()
			.Select(t => t.GetTableName()).ToHashSet(StringComparer.Ordinal);
		var postgresTables = BuildCombinedPostgreSqlModel().GetEntityTypes()
			.Select(t => t.GetTableName()).ToHashSet(StringComparer.Ordinal);

		Assert.Equal(sqliteTables, postgresTables);
	}

	[Fact]
	public void DefaultConfigurationDbContext_AppliesTheNeutralModel()
	{
		var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
			.UseSqlite("Data Source=:memory:")
			.Options;
		using var context = new ConfigurationDbContext(options);

		var mapped = context.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

		Assert.Equal(ConfigurationEntityTypes.ToHashSet(), mapped);
		Assert.Null(context.Model.FindEntityType(typeof(RealmEntity))!.GetSchema());
	}
}
