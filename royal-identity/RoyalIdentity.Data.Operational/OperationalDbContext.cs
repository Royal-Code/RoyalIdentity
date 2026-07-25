using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;

namespace RoyalIdentity.Data.Operational;

/// <summary>
/// Provider-neutral default context for the Operational family. Mappings never live here: the sealed
/// <see cref="OnModelCreating"/> delegates to <see cref="ApplyOperationalModel"/>, which by default applies
/// only the neutral mappings; provider contexts override that hook to call their public provider extension
/// (plan DF1/DF2). Third parties can ignore this context entirely and apply the same extensions to their own
/// <see cref="DbContext"/>, including one combined with the Configuration family.
/// </summary>
public class OperationalDbContext : DbContext
{
    public OperationalDbContext(DbContextOptions<OperationalDbContext> options) : base(options)
    {
    }

    /// <summary>Constructor for derived provider contexts carrying their own options type.</summary>
    protected OperationalDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<ProtocolArtifactEntity> ProtocolArtifacts => Set<ProtocolArtifactEntity>();

    public DbSet<ConsentEntity> Consents => Set<ConsentEntity>();

    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();

    public DbSet<UserSessionClientEntity> UserSessionClients => Set<UserSessionClientEntity>();

    public DbSet<AuthorizeParametersEntity> AuthorizeParameters => Set<AuthorizeParametersEntity>();

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyOperationalModel(modelBuilder);
    }

    /// <summary>
    /// Model hook (plan DF1/DF2): the default applies only the neutral mappings; provider contexts override it
    /// to apply the full public mapping extension of exactly one provider.
    /// </summary>
    protected virtual void ApplyOperationalModel(ModelBuilder modelBuilder)
        => modelBuilder.ApplyRoyalIdentityOperationalMappings(new OperationalModelOptions());
}
