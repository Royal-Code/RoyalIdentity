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
			// Path/domain are reserved even for tombstones (plan DF22): no filter, so a deleted realm keeps
			// its slot. Ordinal comparison is pinned by the provider collation refinement (plan DF23).
			entity.HasIndex(e => e.Path).IsUnique().HasDatabaseName("ux_realms_path");
			entity.HasIndex(e => e.Domain).IsUnique().HasDatabaseName("ux_realms_domain");
		});

		modelBuilder.Entity<ClientEntity>(entity =>
		{
			entity.ToTable("clients", schema);
			entity.HasKey(e => new { e.RealmId, e.ClientId });
			entity.Property(e => e.RealmId).HasColumnName("realm_id");
			entity.Property(e => e.ClientId).HasColumnName("client_id");
			entity.Property(e => e.Name).HasColumnName("name");
			entity.Property(e => e.Description).HasColumnName("description");
			entity.Property(e => e.ClientUri).HasColumnName("client_uri");
			entity.Property(e => e.LogoUri).HasColumnName("logo_uri");
			entity.Property(e => e.Enabled).HasColumnName("enabled");
			entity.Property(e => e.ProtocolType).HasColumnName("protocol_type");
			entity.Property(e => e.RequirePkce).HasColumnName("require_pkce");
			entity.Property(e => e.AllowPlainTextPkce).HasColumnName("allow_plain_text_pkce");
			entity.Property(e => e.ClientType).HasColumnName("client_type");
			entity.Property(e => e.AllowOfflineAccess).HasColumnName("allow_offline_access");
			entity.Property(e => e.AllowAllResourceServers).HasColumnName("allow_all_resource_servers");
			entity.Property(e => e.IncludeJwtId).HasColumnName("include_jwt_id");
			entity.Property(e => e.AlwaysSendClientClaims).HasColumnName("always_send_client_claims");
			entity.Property(e => e.AlwaysIncludeUserClaimsInIdToken).HasColumnName("always_include_user_claims_in_id_token");
			entity.Property(e => e.ClientClaimsPrefix).HasColumnName("client_claims_prefix");
			entity.Property(e => e.EnableLocalLogin).HasColumnName("enable_local_login");
			entity.Property(e => e.UserSsoLifetime).HasColumnName("user_sso_lifetime");
			entity.Property(e => e.AccessTokenLifetime).HasColumnName("access_token_lifetime");
			entity.Property(e => e.IdentityTokenLifetime).HasColumnName("identity_token_lifetime");
			entity.Property(e => e.AuthorizationCodeLifetime).HasColumnName("authorization_code_lifetime");
			entity.Property(e => e.AbsoluteRefreshTokenLifetime).HasColumnName("absolute_refresh_token_lifetime");
			entity.Property(e => e.SlidingRefreshTokenLifetime).HasColumnName("sliding_refresh_token_lifetime");
			entity.Property(e => e.ConsentLifetime).HasColumnName("consent_lifetime");
			entity.Property(e => e.RequireConsent).HasColumnName("require_consent");
			entity.Property(e => e.AllowRememberConsent).HasColumnName("allow_remember_consent");
			entity.Property(e => e.RequireClientSecret).HasColumnName("require_client_secret");
			entity.Property(e => e.RefreshTokenExpiration).HasColumnName("refresh_token_expiration");
			entity.Property(e => e.RefreshTokenPostConsumedTimeToleranceTicks)
				.HasColumnName("refresh_token_post_consumed_time_tolerance_ticks");
			entity.Property(e => e.UpdateAccessTokenClaimsOnRefresh).HasColumnName("update_access_token_claims_on_refresh");
			entity.Property(e => e.AllowLogoutWithoutUserConfirmation).HasColumnName("allow_logout_without_user_confirmation");
			entity.Property(e => e.FrontChannelLogoutSessionRequired).HasColumnName("front_channel_logout_session_required");
			entity.Property(e => e.BackChannelLogoutSessionRequired).HasColumnName("back_channel_logout_session_required");
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
