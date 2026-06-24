using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

public class PasswordLifecycleTests
{
	private static readonly DateTimeOffset Now = new(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
	private static readonly TestPasswordHasher Hasher = new();

	// ---- History recording & pruning ----

	[Fact]
	public void SetPassword_ArchivesPreviousHash_WhenHistoryEnforced()
	{
		var options = HistoryOptions(count: 5);
		var account = NewAccount();

		Assert.True(account.SetPassword(Hasher.Hash("p1"), Now, options, PasswordChangeReason.Create).IsSuccess);
		Assert.Empty(account.PasswordHistory); // first password has nothing to archive

		Assert.True(account.SetPassword(Hasher.Hash("p2"), Now.AddDays(1), options).IsSuccess);

		var entry = Assert.Single(account.PasswordHistory);
		Assert.Equal(Hasher.Hash("p1"), entry.PasswordHash);
		Assert.Equal(PasswordChangeReason.Change, entry.Reason);
	}

	[Fact]
	public void SetPassword_DoesNotArchive_WhenHistoryDisabled()
	{
		var options = HistoryOptions(count: 5);
		options.EnforcePasswordHistory = false;
		var account = NewAccount();

		Assert.True(account.SetPassword(Hasher.Hash("p1"), Now, options).IsSuccess);
		Assert.True(account.SetPassword(Hasher.Hash("p2"), Now.AddDays(1), options).IsSuccess);

		Assert.Empty(account.PasswordHistory);
	}

	[Fact]
	public void SetPassword_PrunesHistory_ToRetainedQuantity()
	{
		var options = HistoryOptions(count: 2);
		var account = NewAccount();

		// Set 5 passwords; each change archives the previous one.
		for (var i = 1; i <= 5; i++)
		{
			Assert.True(account.SetPassword(Hasher.Hash($"p{i}"), Now.AddDays(i), options).IsSuccess);
		}

		// Only the 2 most recently archived hashes (p3, p4) are retained; p1/p2 pruned.
		var retained = account.PasswordHistory.Select(h => h.PasswordHash).ToHashSet();
		Assert.Equal(2, retained.Count);
		Assert.Contains(Hasher.Hash("p3"), retained);
		Assert.Contains(Hasher.Hash("p4"), retained);
	}

	[Fact]
	public void SetPassword_RetainsHistory_WithinAgeWindow_BeyondQuantity()
	{
		// Quantity is only 1, but the 365-day age window keeps every recent archive.
		var options = HistoryOptions(count: 1, reuseWindowDays: 365);
		var account = NewAccount();

		Assert.True(account.SetPassword(Hasher.Hash("p1"), Now, options, PasswordChangeReason.Create).IsSuccess);
		Assert.True(account.SetPassword(Hasher.Hash("p2"), Now.AddDays(10), options).IsSuccess);
		Assert.True(account.SetPassword(Hasher.Hash("p3"), Now.AddDays(20), options).IsSuccess);

		var retained = account.PasswordHistory.Select(h => h.PasswordHash).ToHashSet();
		Assert.Contains(Hasher.Hash("p1"), retained);
		Assert.Contains(Hasher.Hash("p2"), retained);
	}

	[Fact]
	public void SetPassword_PrunesEntries_OutsideAgeWindowAndQuantity()
	{
		var options = HistoryOptions(count: 1, reuseWindowDays: 30);
		var account = NewAccount();

		Assert.True(account.SetPassword(Hasher.Hash("p1"), Now, options, PasswordChangeReason.Create).IsSuccess);
		// archive p1 early (history CreatedAt is the archival time)
		Assert.True(account.SetPassword(Hasher.Hash("p2"), Now.AddDays(1), options).IsSuccess);
		// archive p2 much later; p1's archival (+1d) now falls outside quantity (count 1) and the 30-day window
		Assert.True(account.SetPassword(Hasher.Hash("p3"), Now.AddDays(100), options).IsSuccess);

		var retained = account.PasswordHistory.Select(h => h.PasswordHash).ToHashSet();
		Assert.Contains(Hasher.Hash("p2"), retained);
		Assert.DoesNotContain(Hasher.Hash("p1"), retained);
	}

	// ---- Reuse policy ----

	[Fact]
	public void ReusePolicy_RejectsCurrentPassword()
	{
		var options = HistoryOptions(count: 3);
		var account = NewAccount();
		Assert.True(account.SetPassword(Hasher.Hash("current"), Now, options, PasswordChangeReason.Create).IsSuccess);

		var result = new PasswordHistoryPolicy().Validate("current", account, options, Hasher, Now.AddDays(1));

		Assert.True(result.IsFailure);
	}

	[Fact]
	public void ReusePolicy_RejectsRecentHistoricalPassword_AndAllowsPruned()
	{
		var options = HistoryOptions(count: 2);
		var account = NewAccount();
		for (var i = 1; i <= 5; i++)
		{
			Assert.True(account.SetPassword(Hasher.Hash($"p{i}"), Now.AddDays(i), options).IsSuccess);
		}

		var policy = new PasswordHistoryPolicy();
		var at = Now.AddDays(10);

		// p5 is current; p3/p4 retained in history -> reuse rejected
		Assert.True(policy.Validate("p5", account, options, Hasher, at).IsFailure);
		Assert.True(policy.Validate("p4", account, options, Hasher, at).IsFailure);
		Assert.True(policy.Validate("p3", account, options, Hasher, at).IsFailure);

		// p1/p2 were pruned -> allowed again
		Assert.True(policy.Validate("p1", account, options, Hasher, at).IsSuccess);
		Assert.True(policy.Validate("p2", account, options, Hasher, at).IsSuccess);

		// a never-used password is allowed
		Assert.True(policy.Validate("brand-new", account, options, Hasher, at).IsSuccess);
	}

	[Fact]
	public void ReusePolicy_IsDisabled_WhenHistoryOff()
	{
		var options = HistoryOptions(count: 3);
		options.EnforcePasswordHistory = false;
		var account = NewAccount();
		Assert.True(account.SetPassword(Hasher.Hash("current"), Now, options, PasswordChangeReason.Create).IsSuccess);

		var result = new PasswordHistoryPolicy().Validate("current", account, options, Hasher, Now.AddDays(1));

		Assert.True(result.IsSuccess);
	}

	// ---- Expiration detection ----

	[Fact]
	public void IsPasswordExpired_DetectsExpiration_ByPolicy()
	{
		var options = HistoryOptions(count: 3);
		options.EnablePasswordExpiration = true;
		options.PasswordExpirationDays = 10;
		var account = NewAccount();
		Assert.True(account.SetPassword(Hasher.Hash("p1"), Now, options, PasswordChangeReason.Create).IsSuccess);

		Assert.False(account.LocalCredential.IsPasswordExpired(options, Now.AddDays(9)));
		Assert.True(account.LocalCredential.IsPasswordExpired(options, Now.AddDays(11)));
	}

	[Fact]
	public void IsPasswordExpired_IsFalse_WhenExpirationDisabled_OrNoPassword()
	{
		var disabled = HistoryOptions(count: 3);
		disabled.EnablePasswordExpiration = false;
		disabled.PasswordExpirationDays = 10;

		var withPassword = NewAccount();
		Assert.True(withPassword.SetPassword(Hasher.Hash("p1"), Now, disabled, PasswordChangeReason.Create).IsSuccess);
		Assert.False(withPassword.LocalCredential.IsPasswordExpired(disabled, Now.AddDays(100)));

		var enabled = HistoryOptions(count: 3);
		enabled.EnablePasswordExpiration = true;
		enabled.PasswordExpirationDays = 10;
		var noPassword = NewAccount();
		Assert.False(noPassword.LocalCredential.IsPasswordExpired(enabled, Now.AddDays(100)));
	}

	private static UserAccount NewAccount()
	{
		return new UserAccount("realm-a", "subject-1", "alice", "ALICE", "Alice", Now);
	}

	private static PasswordOptions HistoryOptions(int count, int reuseWindowDays = 0)
	{
		return new PasswordOptions
		{
			EnforcePasswordHistory = true,
			PasswordHistoryCount = count,
			PasswordReuseWindowDays = reuseWindowDays,
			MaxPasswordHistoryComparisons = 24
		};
	}

	private sealed class TestPasswordHasher : IUserAccountPasswordHasher
	{
		public string Hash(string password) => $"hashed:{password}";

		public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
	}
}
