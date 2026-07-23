using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Data.Configuration.Entities;

namespace RoyalIdentity.Storage.EntityFramework.PostgreSql;

/// <summary>
/// Public PostgreSQL mapping extension (plan DF3): applies the neutral Configuration mappings plus the
/// PostgreSQL refinements — the <c>configuration</c> schema (plan DF18) and <c>jsonb</c> payloads. Default
/// and custom/combined contexts call this same extension; a model applies exactly one provider extension.
/// </summary>
public static class PostgreSqlConfigurationModelBuilderExtensions
{
	public static ModelBuilder ApplyRoyalIdentityConfigurationPostgreSqlMappings(
		this ModelBuilder modelBuilder, ConfigurationModelOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		options ??= new ConfigurationModelOptions();
		// DF18: PostgreSQL uses the `configuration` schema unless the consumer overrides it.
		options.Schema ??= "configuration";

		modelBuilder.ApplyRoyalIdentityConfigurationMappings(options);

		modelBuilder.Entity<ServerOptionsEntity>().Property(e => e.PayloadJson).HasColumnType("jsonb");
		modelBuilder.Entity<RealmEntity>().Property(e => e.OptionsJson).HasColumnType("jsonb");

		// PostgreSQL's database default is deployment-defined. Pin identifier keys and unique values to the
		// built-in byte-wise C collation so Ordinal semantics do not drift with database locale (plan DF23).
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

	private const string Ordinal = "C";
}
