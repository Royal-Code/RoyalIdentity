using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="PropertyDefinition"/> (stable claim-type identity).
/// </summary>
public sealed class PropertyDefinitionMap : IEntityTypeConfiguration<PropertyDefinition>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PropertyDefinition> builder)
	{
		builder.ToTable("PropertyDefinitions");

		builder.HasKey(d => d.Id);
		builder.Property(d => d.Id).ValueGeneratedOnAdd();

		builder.Property(d => d.RealmId).IsRequired();
		builder.Property(d => d.PropertyScopeId).IsRequired();
		builder.Property(d => d.ClaimType).IsRequired();

		builder.HasIndex(d => new { d.RealmId, d.ClaimType }).IsUnique();
		builder.HasIndex(d => new { d.RealmId, d.PropertyScopeId });
	}
}
