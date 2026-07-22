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

		// JSON payloads are TEXT columns on SQLite (validation is a provider concern deferred to later phases).
		modelBuilder.Entity<ServerOptionsEntity>().Property(e => e.PayloadJson).HasColumnType("TEXT");
		modelBuilder.Entity<RealmEntity>().Property(e => e.OptionsJson).HasColumnType("TEXT");

		// DF23: pin Ordinal (case-sensitive, byte-wise) comparison on every identifier that backs a primary
		// key or a unique index, so uniqueness never silently depends on the provider's default collation.
		// SQLite's default happens to be BINARY, but declaring it makes the guarantee explicit and testable.
		modelBuilder.Entity<RealmEntity>(entity =>
		{
			entity.Property(e => e.Id).UseCollation(Ordinal);
			entity.Property(e => e.Path).UseCollation(Ordinal);
			entity.Property(e => e.Domain).UseCollation(Ordinal);
		});

		modelBuilder.Entity<ClientEntity>(entity =>
		{
			entity.Property(e => e.RealmId).UseCollation(Ordinal);
			entity.Property(e => e.ClientId).UseCollation(Ordinal);
		});

		modelBuilder.Entity<ClientStringValueEntity>(entity =>
		{
			entity.Property(e => e.RealmId).UseCollation(Ordinal);
			entity.Property(e => e.ClientId).UseCollation(Ordinal);
			entity.Property(e => e.Kind).UseCollation(Ordinal);
			entity.Property(e => e.ComparisonKey).UseCollation(Ordinal);
		});

		modelBuilder.Entity<ClientClaimEntity>(entity =>
		{
			entity.Property(e => e.RealmId).UseCollation(Ordinal);
			entity.Property(e => e.ClientId).UseCollation(Ordinal);
		});

		modelBuilder.Entity<ClientSecretEntity>(entity =>
		{
			entity.Property(e => e.RealmId).UseCollation(Ordinal);
			entity.Property(e => e.ClientId).UseCollation(Ordinal);
		});

		modelBuilder.Entity<SigningKeyEntity>(entity =>
		{
			entity.Property(e => e.RealmId).UseCollation(Ordinal);
			entity.Property(e => e.KeyId).UseCollation(Ordinal);
		});

		return modelBuilder;
	}

	/// <summary>SQLite's byte-wise (case-sensitive) collation, the Ordinal equivalent (plan DF23).</summary>
	private const string Ordinal = "BINARY";
}
