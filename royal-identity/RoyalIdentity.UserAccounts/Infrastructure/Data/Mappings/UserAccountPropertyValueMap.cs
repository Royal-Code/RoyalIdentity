using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="UserAccountPropertyValue"/>.
/// </summary>
public sealed class UserAccountPropertyValueMap : IEntityTypeConfiguration<UserAccountPropertyValue>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccountPropertyValue> builder)
	{
		builder.ToTable("UserAccountPropertyValues");

		builder.HasKey(v => v.Id);
		builder.Property(v => v.Id).ValueGeneratedOnAdd();

		builder.Property(v => v.RealmId).IsRequired();
		builder.Property(v => v.UserAccountId).IsRequired();
		builder.Property(v => v.PropertyDefinitionId).IsRequired();
		builder.Property(v => v.ClaimType).IsRequired();
		builder.Property(v => v.Value).IsRequired();
		builder.Property(v => v.ValueType).HasConversion<string>().IsRequired();
		builder.Property(v => v.Ordinal).IsRequired();

		builder.HasIndex(v => new { v.UserAccountId, v.PropertyDefinitionId, v.Ordinal }).IsUnique();
		builder.HasIndex(v => new { v.RealmId, v.UserAccountId, v.ClaimType });
		builder.HasIndex(v => new { v.RealmId, v.ClaimType });

		// Cross-aggregate link to the stable definition; the value lifecycle follows the account, not the definition.
		builder.HasOne(v => v.PropertyDefinition)
			.WithMany()
			.HasForeignKey(v => v.PropertyDefinitionId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
