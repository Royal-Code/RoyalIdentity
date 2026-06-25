using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for single-use account action tokens. Tokens have an independent lifecycle (issued and consumed
/// out of band from the account graph), so they are mapped as a standalone entity with a foreign key to the account
/// but no navigation on either side — they are never loaded with the aggregate.
/// </summary>
public sealed class UserAccountActionTokenMap : IEntityTypeConfiguration<UserAccountActionToken>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccountActionToken> builder)
	{
		builder.ToTable("UserAccountActionTokens");

		builder.HasKey(t => t.Id);
		builder.Property(t => t.Id).ValueGeneratedOnAdd();

		builder.Property(t => t.RealmId).IsRequired();
		builder.Property(t => t.UserAccountId).IsRequired();
		builder.Property(t => t.Purpose)
			.HasConversion<string>()
			.IsRequired();
		builder.Property(t => t.TokenHash).IsRequired();
		builder.Property(t => t.TargetValue);

		// Stored as UTC ticks so the TTL/throttle comparisons translate to SQL on every provider (the SQLite
		// provider cannot translate DateTimeOffset ordering). All timestamps in this module are UTC.
		builder.Property(t => t.CreatedAt)
			.HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
			.IsRequired();
		builder.Property(t => t.ExpiresAt)
			.HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
			.IsRequired();
		builder.Property(t => t.ConsumedAt);
		builder.Property(t => t.RevokedAt);
		builder.Property(t => t.RevokedReason)
			.HasConversion<string>();
		builder.Property(t => t.CreatedIpHash);
		builder.Property(t => t.ConsumedIpHash);
		builder.Property(t => t.UserAgentHash);

		builder.HasIndex(t => new { t.RealmId, t.UserAccountId, t.Purpose });
		builder.HasIndex(t => new { t.RealmId, t.TokenHash }).IsUnique();

		builder.HasOne<UserAccount>()
			.WithMany()
			.HasForeignKey(t => t.UserAccountId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
