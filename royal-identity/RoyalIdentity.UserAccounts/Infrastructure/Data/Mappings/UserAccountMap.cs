using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for the <see cref="UserAccount"/> aggregate root.
/// </summary>
public sealed class UserAccountMap : IEntityTypeConfiguration<UserAccount>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccount> builder)
	{
		builder.ToTable("UserAccounts");

		builder.HasKey(a => a.Id);
		builder.Property(a => a.Id).ValueGeneratedOnAdd();

		builder.Property(a => a.RealmId).IsRequired();
		builder.Property(a => a.SubjectId).IsRequired();
		builder.Property(a => a.Username).IsRequired();
		builder.Property(a => a.NormalizedUsername).IsRequired();
		builder.Property(a => a.DisplayName).IsRequired();
		builder.Property(a => a.IsActive).IsRequired();
		builder.Property(a => a.ExternalId);
		builder.Property(a => a.CreatedAt).IsRequired();
		builder.Property(a => a.UpdatedAt).IsRequired();
		builder.Property(a => a.SecurityStamp)
			.HasConversion(
				stamp => stamp.Value,
				value => SecurityStamp.FromPersisted(value))
			.IsRequired();
		builder.Property(a => a.SessionsValidAfter).IsRequired();
		builder.Property(a => a.Version).IsConcurrencyToken();

		// Administrative block state stored inline on the account row.
		builder.OwnsOne(a => a.BlockState, block =>
		{
			block.Property(b => b.IsBlocked).HasColumnName("IsBlocked").IsRequired();
			block.Property(b => b.BlockedReason).HasColumnName("BlockedReason");
			block.Property(b => b.BlockedAt).HasColumnName("BlockedAt");
		});
		builder.Navigation(a => a.BlockState).IsRequired();

		// Computed projections over BlockState / collections must not be mapped.
		builder.Ignore(a => a.IsBlocked);
		builder.Ignore(a => a.BlockedReason);
		builder.Ignore(a => a.BlockedAt);
		builder.Ignore(a => a.PrimaryEmail);
		builder.Ignore(a => a.Emails);
		builder.Ignore(a => a.Roles);
		builder.Ignore(a => a.PropertyValues);
		builder.Ignore(a => a.PasswordHistory);
		builder.Ignore(a => a.DomainEvents);

		builder.HasIndex(a => new { a.RealmId, a.SubjectId }).IsUnique();
		builder.HasIndex(a => new { a.RealmId, a.NormalizedUsername }).IsUnique();
		builder.HasIndex(a => new { a.RealmId, a.ExternalId });

		builder.HasMany<UserAccountEmail>("EmailItems")
			.WithOne(e => e.UserAccount!)
			.HasForeignKey(e => e.UserAccountId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany<UserAccountRole>("RoleItems")
			.WithOne(r => r.UserAccount!)
			.HasForeignKey(r => r.UserAccountId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany<UserAccountPropertyValue>("PropertyValueItems")
			.WithOne(v => v.UserAccount!)
			.HasForeignKey(v => v.UserAccountId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany<PasswordHistoryEntry>("PasswordHistoryItems")
			.WithOne(h => h.UserAccount!)
			.HasForeignKey(h => h.UserAccountId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(a => a.LocalCredential)
			.WithOne(c => c.UserAccount!)
			.HasForeignKey<UserAccountCredential>(c => c.UserAccountId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(a => a.LocalCredential).IsRequired();
	}
}
