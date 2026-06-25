using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="UserAccountPhone"/>.
/// </summary>
public sealed class UserAccountPhoneMap : IEntityTypeConfiguration<UserAccountPhone>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccountPhone> builder)
	{
		builder.ToTable("UserAccountPhones");

		builder.HasKey(p => p.Id);
		builder.Property(p => p.Id).ValueGeneratedOnAdd();

		builder.Property(p => p.RealmId).IsRequired();
		builder.Property(p => p.UserAccountId).IsRequired();
		builder.Property(p => p.Number).IsRequired();
		builder.Property(p => p.NormalizedNumber).IsRequired();
		builder.Property(p => p.IsPrimary).IsRequired();
		builder.Property(p => p.IsVerified).IsRequired();

		builder.HasIndex(p => new { p.RealmId, p.UserAccountId });
		builder.HasIndex(p => new { p.RealmId, p.UserAccountId, p.NormalizedNumber }).IsUnique();
		builder.HasIndex(p => new { p.RealmId, p.NormalizedNumber });
	}
}
