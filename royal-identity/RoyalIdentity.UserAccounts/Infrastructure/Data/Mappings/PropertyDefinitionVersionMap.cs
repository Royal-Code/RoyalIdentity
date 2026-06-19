using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="PropertyDefinitionVersion"/> (versioned settings + declarative rules).
/// </summary>
public sealed class PropertyDefinitionVersionMap : IEntityTypeConfiguration<PropertyDefinitionVersion>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PropertyDefinitionVersion> builder)
	{
		builder.ToTable("PropertyDefinitionVersions");

		builder.HasKey(dv => dv.Id);
		builder.Property(dv => dv.Id).ValueGeneratedOnAdd();

		builder.Property(dv => dv.RealmId).IsRequired();
		builder.Property(dv => dv.PropertyScopeVersionId).IsRequired();
		builder.Property(dv => dv.PropertyDefinitionId).IsRequired();
		builder.Property(dv => dv.ClaimType).IsRequired();
		builder.Property(dv => dv.ValueType).HasConversion<string>().IsRequired();
		builder.Property(dv => dv.DisplayName);
		builder.Property(dv => dv.Help);
		builder.Property(dv => dv.IsSensitive).IsRequired();
		builder.Property(dv => dv.IsRequired).IsRequired();
		builder.Property(dv => dv.IsCollection).IsRequired();
		builder.Property(dv => dv.IsActive).IsRequired();

		var rules = builder.Property(dv => dv.ValidationRules)
			.HasConversion(new PropertyValidationRulesConverter())
			.IsRequired();
		rules.Metadata.SetValueComparer(PropertyValidationRulesConverter.Comparer);

		builder.HasIndex(dv => new { dv.PropertyScopeVersionId, dv.PropertyDefinitionId }).IsUnique();
		builder.HasIndex(dv => new { dv.RealmId, dv.PropertyDefinitionId });

		builder.HasOne(dv => dv.PropertyDefinition)
			.WithMany()
			.HasForeignKey(dv => dv.PropertyDefinitionId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
