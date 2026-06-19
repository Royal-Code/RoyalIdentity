using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for <see cref="UserAccountEmail"/>.
/// </summary>
public sealed class UserAccountEmailMap : IEntityTypeConfiguration<UserAccountEmail>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccountEmail> builder)
	{
		builder.ToTable("UserAccountEmails");

		builder.HasKey(e => e.Id);
		builder.Property(e => e.Id).ValueGeneratedOnAdd();

		builder.Property(e => e.RealmId).IsRequired();
		builder.Property(e => e.UserAccountId).IsRequired();
		builder.Property(e => e.Address).IsRequired();
		builder.Property(e => e.NormalizedAddress).IsRequired();
		builder.Property(e => e.IsPrimary).IsRequired();
		builder.Property(e => e.IsVerified).IsRequired();
		builder.Property(e => e.IsFictitious).IsRequired();

		builder.HasIndex(e => new { e.RealmId, e.UserAccountId });
		builder.HasIndex(e => new { e.RealmId, e.UserAccountId, e.NormalizedAddress }).IsUnique();
		builder.HasIndex(e => new { e.RealmId, e.NormalizedAddress });
	}
}
