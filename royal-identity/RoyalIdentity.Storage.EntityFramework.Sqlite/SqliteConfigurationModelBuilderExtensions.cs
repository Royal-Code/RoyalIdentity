using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Configuration.Entities;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// Public SQLite mapping extension (plan DF3): applies the neutral Configuration mappings plus the SQLite
/// refinements. Default and custom/combined contexts call this same extension; a model applies exactly one
/// provider extension.
/// </summary>
public static class SqliteConfigurationModelBuilderExtensions
{
	public static ModelBuilder ApplyRoyalIdentityConfigurationSqliteMappings(
		this ModelBuilder modelBuilder, ConfigurationModelOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		options ??= new ConfigurationModelOptions();
		// DF18: SQLite uses the same table names without schema.
		options.Schema = null;

		modelBuilder.ApplyRoyalIdentityConfigurationMappings(options);

		// JSON payloads are TEXT columns on SQLite; validation, when supported, is added with the
		// Fase 2 model work.
		modelBuilder.Entity<ServerOptionsEntity>().Property(e => e.PayloadJson).HasColumnType("TEXT");
		modelBuilder.Entity<RealmEntity>().Property(e => e.OptionsJson).HasColumnType("TEXT");

		return modelBuilder;
	}
}
