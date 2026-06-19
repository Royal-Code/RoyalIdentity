using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="UserAccountRole"/>.
/// </summary>
public sealed class UserAccountRoleMap : IEntityTypeConfiguration<UserAccountRole>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccountRole> builder)
	{
		builder.ToTable("UserAccountRoles");

		builder.HasKey(r => r.Id);
		builder.Property(r => r.Id).ValueGeneratedOnAdd();

		builder.Property(r => r.RealmId).IsRequired();
		builder.Property(r => r.UserAccountId).IsRequired();
		builder.Property(r => r.Name).IsRequired();
		builder.Property(r => r.NormalizedName).IsRequired();

		builder.HasIndex(r => new { r.RealmId, r.UserAccountId, r.NormalizedName }).IsUnique();
		builder.HasIndex(r => new { r.RealmId, r.NormalizedName });
	}
}
