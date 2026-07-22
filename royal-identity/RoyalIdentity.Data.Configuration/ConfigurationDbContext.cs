using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration.Entities;

namespace RoyalIdentity.Data.Configuration;

/// <summary>
/// Provider-neutral default context for the Configuration family. Mappings never live here: the sealed
/// <see cref="OnModelCreating"/> delegates to <see cref="ApplyConfigurationModel"/>, which by default
/// applies only the neutral mappings; provider contexts override that hook to call their public provider
/// extension (plan DF3). Third parties can ignore this context entirely and apply the same extensions to
/// their own <see cref="DbContext"/>, including one combined with the future Operational family.
/// </summary>
public class ConfigurationDbContext : DbContext
{
	public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : base(options)
	{
	}

	/// <summary>Constructor for derived provider contexts carrying their own options type.</summary>
	protected ConfigurationDbContext(DbContextOptions options) : base(options)
	{
	}

	public DbSet<ServerOptionsEntity> ServerOptions => Set<ServerOptionsEntity>();

	public DbSet<RealmEntity> Realms => Set<RealmEntity>();

	public DbSet<ClientEntity> Clients => Set<ClientEntity>();

	public DbSet<ClientStringValueEntity> ClientStringValues => Set<ClientStringValueEntity>();

	public DbSet<ClientClaimEntity> ClientClaims => Set<ClientClaimEntity>();

	public DbSet<ClientSecretEntity> ClientSecrets => Set<ClientSecretEntity>();

	public DbSet<SigningKeyEntity> SigningKeys => Set<SigningKeyEntity>();

	protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		ApplyConfigurationModel(modelBuilder);
	}

	/// <summary>
	/// Model hook (plan DF3): the default applies only the neutral mappings; provider contexts override it
	/// to apply the full public mapping extension of exactly one provider.
	/// </summary>
	protected virtual void ApplyConfigurationModel(ModelBuilder modelBuilder)
		=> modelBuilder.ApplyRoyalIdentityConfigurationMappings(new ConfigurationModelOptions());
}
