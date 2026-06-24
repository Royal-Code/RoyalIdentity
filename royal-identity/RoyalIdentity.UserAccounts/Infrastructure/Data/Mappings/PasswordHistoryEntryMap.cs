using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for archived password history entries.
/// </summary>
public sealed class PasswordHistoryEntryMap : IEntityTypeConfiguration<PasswordHistoryEntry>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
	{
		builder.ToTable("UserAccountPasswordHistory");

		builder.HasKey(h => h.Id);
		builder.Property(h => h.Id).ValueGeneratedOnAdd();

		builder.Property(h => h.RealmId).IsRequired();
		builder.Property(h => h.UserAccountId).IsRequired();
		builder.Property(h => h.PasswordHash).IsRequired();
		builder.Property(h => h.CreatedAt).IsRequired();
		builder.Property(h => h.Reason).IsRequired();
		builder.Property(h => h.CreatedBySubjectId);

		builder.HasIndex(h => new { h.RealmId, h.UserAccountId, h.CreatedAt });
	}
}
