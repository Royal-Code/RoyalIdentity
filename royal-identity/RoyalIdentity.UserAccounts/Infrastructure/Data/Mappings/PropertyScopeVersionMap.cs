using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="PropertyScopeVersion"/>.
/// </summary>
public sealed class PropertyScopeVersionMap : IEntityTypeConfiguration<PropertyScopeVersion>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PropertyScopeVersion> builder)
	{
		builder.ToTable("PropertyScopeVersions");

		builder.HasKey(v => v.Id);
		builder.Property(v => v.Id).ValueGeneratedOnAdd();

		builder.Property(v => v.RealmId).IsRequired();
		builder.Property(v => v.PropertyScopeId).IsRequired();
		builder.Property(v => v.Version).IsRequired();
		builder.Property(v => v.Status).HasConversion<string>().IsRequired();
		builder.Property(v => v.DisplayName);
		builder.Property(v => v.CreatedAt).IsRequired();
		builder.Property(v => v.ApprovedAt);

		builder.Ignore(v => v.DefinitionVersions);

		builder.HasIndex(v => new { v.RealmId, v.PropertyScopeId, v.Version }).IsUnique();
		builder.HasIndex(v => new { v.RealmId, v.PropertyScopeId, v.Status });

		builder.HasMany<PropertyDefinitionVersion>("DefinitionVersionItems")
			.WithOne(dv => dv.PropertyScopeVersion!)
			.HasForeignKey(dv => dv.PropertyScopeVersionId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
