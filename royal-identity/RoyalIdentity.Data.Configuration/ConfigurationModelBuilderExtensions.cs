using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration.Entities;

namespace RoyalIdentity.Data.Configuration;

/// <summary>
/// Provider-neutral Configuration mappings (plan DF3). Contexts never carry mappings themselves: the
/// default/provider contexts and third-party combined contexts all call a public extension — this neutral
/// one directly, or a provider extension that composes it with provider refinements. Unique indexes,
/// comparators and payload serialization details land in Fase 2.
/// </summary>
public static class ConfigurationModelBuilderExtensions
{
	public static ModelBuilder ApplyRoyalIdentityConfigurationMappings(
		this ModelBuilder modelBuilder, ConfigurationModelOptions options)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);
		ArgumentNullException.ThrowIfNull(options);

		var schema = options.Schema;

		modelBuilder.Entity<ServerOptionsEntity>(entity =>
		{
			entity.ToTable("server_options", schema);
			entity.ToTable(t => t.HasCheckConstraint("ck_server_options_singleton", "id = 1"));
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
			entity.Property(e => e.PayloadVersion).HasColumnName("payload_version");
			entity.Property(e => e.PayloadJson).HasColumnName("payload_json");
			entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");
		});

		modelBuilder.Entity<RealmEntity>(entity =>
		{
			entity.ToTable("realms", schema);
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
			entity.Property(e => e.Path).HasColumnName("path");
			entity.Property(e => e.Domain).HasColumnName("domain");
			entity.Property(e => e.DisplayName).HasColumnName("display_name");
			entity.Property(e => e.Enabled).HasColumnName("enabled");
			entity.Property(e => e.Internal).HasColumnName("internal");
			entity.Property(e => e.OptionsVersion).HasColumnName("options_version");
			entity.Property(e => e.OptionsJson).HasColumnName("options_json");
			entity.Property(e => e.DeletedAtUtc).HasColumnName("deleted_at_utc");
		});

		modelBuilder.Entity<ClientEntity>(entity =>
		{
			entity.ToTable("clients", schema);
			entity.HasKey(e => new { e.RealmId, e.ClientId });
			entity.Property(e => e.RealmId).HasColumnName("realm_id");
			entity.Property(e => e.ClientId).HasColumnName("client_id");
			entity.Property(e => e.Name).HasColumnName("name");
			entity.Property(e => e.Enabled).HasColumnName("enabled");
			entity.HasOne<RealmEntity>()
				.WithMany()
				.HasForeignKey(e => e.RealmId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<ClientStringValueEntity>(entity =>
		{
			entity.ToTable("client_string_values", schema);
			entity.HasKey(e => new { e.RealmId, e.ClientId, e.Kind, e.ComparisonKey });
			entity.Property(e => e.RealmId).HasColumnName("realm_id");
			entity.Property(e => e.ClientId).HasColumnName("client_id");
			entity.Property(e => e.Kind).HasColumnName("kind");
			entity.Property(e => e.Value).HasColumnName("value");
			entity.Property(e => e.ComparisonKey).HasColumnName("comparison_key");
			entity.HasOne<ClientEntity>()
				.WithMany()
				.HasForeignKey(e => new { e.RealmId, e.ClientId })
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<ClientClaimEntity>(entity =>
		{
			entity.ToTable("client_claims", schema);
			entity.HasKey(e => new { e.RealmId, e.ClientId, e.Ordinal });
			entity.Property(e => e.RealmId).HasColumnName("realm_id");
			entity.Property(e => e.ClientId).HasColumnName("client_id");
			entity.Property(e => e.Ordinal).HasColumnName("ordinal").ValueGeneratedNever();
			entity.Property(e => e.Type).HasColumnName("type");
			entity.Property(e => e.Value).HasColumnName("value");
			entity.Property(e => e.ValueType).HasColumnName("value_type");
			entity.Property(e => e.Issuer).HasColumnName("issuer");
			entity.Property(e => e.OriginalIssuer).HasColumnName("original_issuer");
			entity.HasOne<ClientEntity>()
				.WithMany()
				.HasForeignKey(e => new { e.RealmId, e.ClientId })
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<ClientSecretEntity>(entity =>
		{
			entity.ToTable("client_secrets", schema);
			entity.HasKey(e => new { e.RealmId, e.ClientId, e.Ordinal });
			entity.Property(e => e.RealmId).HasColumnName("realm_id");
			entity.Property(e => e.ClientId).HasColumnName("client_id");
			entity.Property(e => e.Ordinal).HasColumnName("ordinal").ValueGeneratedNever();
			entity.Property(e => e.Type).HasColumnName("type");
			entity.Property(e => e.Value).HasColumnName("value");
			entity.Property(e => e.Description).HasColumnName("description");
			entity.Property(e => e.ExpirationUtc).HasColumnName("expiration_utc");
			entity.HasOne<ClientEntity>()
				.WithMany()
				.HasForeignKey(e => new { e.RealmId, e.ClientId })
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<SigningKeyEntity>(entity =>
		{
			entity.ToTable("signing_keys", schema);
			entity.HasKey(e => new { e.RealmId, e.KeyId });
			entity.Property(e => e.RealmId).HasColumnName("realm_id");
			entity.Property(e => e.KeyId).HasColumnName("key_id");
			entity.Property(e => e.Name).HasColumnName("name");
			entity.Property(e => e.SecurityAlgorithm).HasColumnName("security_algorithm");
			entity.Property(e => e.SerializationFormat).HasColumnName("serialization_format");
			entity.Property(e => e.Encoding).HasColumnName("encoding");
			entity.Property(e => e.CreatedUtc).HasColumnName("created_utc");
			entity.Property(e => e.NotBeforeUtc).HasColumnName("not_before_utc");
			entity.Property(e => e.ExpiresUtc).HasColumnName("expires_utc");
			entity.Property(e => e.ProtectorId).HasColumnName("protector_id");
			entity.Property(e => e.ProtectedMaterial).HasColumnName("protected_material");
			entity.HasIndex(e => new { e.RealmId, e.CreatedUtc }).HasDatabaseName("ix_signing_keys_realm_created");
			entity.HasOne<RealmEntity>()
				.WithMany()
				.HasForeignKey(e => e.RealmId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		return modelBuilder;
	}
}
