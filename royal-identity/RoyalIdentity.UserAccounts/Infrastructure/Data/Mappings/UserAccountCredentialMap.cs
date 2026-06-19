using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data.Mappings;

/// <summary>
/// EF Core mapping for the 1:1 local password credential.
/// </summary>
public sealed class UserAccountCredentialMap : IEntityTypeConfiguration<UserAccountCredential>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserAccountCredential> builder)
	{
		builder.ToTable("UserAccountCredentials");

		builder.HasKey(c => c.UserAccountId);
		builder.Property(c => c.UserAccountId).ValueGeneratedNever();

		builder.Property(c => c.RealmId).IsRequired();
		builder.Property(c => c.PasswordHash);
		builder.Property(c => c.PasswordChangedAt);
		builder.Property(c => c.MustChangePassword).IsRequired();
		builder.Property(c => c.FailedPasswordAttempts).IsRequired();
		builder.Property(c => c.LastPasswordFailureAt);
		builder.Property(c => c.LockoutEndAt);

		builder.Ignore(c => c.HasPassword);

		builder.HasIndex(c => new { c.RealmId, c.UserAccountId });
	}
}
