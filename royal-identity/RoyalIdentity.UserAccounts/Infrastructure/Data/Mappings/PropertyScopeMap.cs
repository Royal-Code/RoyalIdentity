using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for the <see cref="PropertyScope"/> aggregate root.
/// </summary>
public sealed class PropertyScopeMap : IEntityTypeConfiguration<PropertyScope>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PropertyScope> builder)
	{
		builder.ToTable("PropertyScopes");

		builder.HasKey(s => s.Id);
		builder.Property(s => s.Id).ValueGeneratedOnAdd();

		builder.Property(s => s.RealmId).IsRequired();
		builder.Property(s => s.Name).IsRequired();
		builder.Property(s => s.IsActive).IsRequired();
		builder.Property(s => s.ActiveVersionId);

		builder.Ignore(s => s.ActiveVersion);
		builder.Ignore(s => s.Versions);
		builder.Ignore(s => s.Definitions);
		builder.Ignore(s => s.DomainEvents);

		builder.HasIndex(s => new { s.RealmId, s.Name }).IsUnique();

		builder.HasMany<PropertyScopeVersion>("VersionItems")
			.WithOne(v => v.PropertyScope!)
			.HasForeignKey(v => v.PropertyScopeId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany<PropertyDefinition>("DefinitionItems")
			.WithOne(d => d.PropertyScope!)
			.HasForeignKey(d => d.PropertyScopeId)
			.OnDelete(DeleteBehavior.Cascade);

		// Denormalized pointer to the active version, kept as a plain column to avoid a circular FK between
		// PropertyScope and PropertyScopeVersion. UserAccountsDbContext reconciles it after keys are generated.
		builder.HasIndex(s => s.ActiveVersionId);
	}
}
