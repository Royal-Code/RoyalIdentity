using RoyalIdentity.UserAccounts.Options;

namespace Tests.UserAccounts;

public class UserAccountsRealmOptionsTests
{
	[Fact]
	public void CopyConstructor_CreatesIndependentCopy()
	{
		var source = new UserAccountsRealmOptions
		{
			AllowRegistration = true,
			AllowForgotPassword = false,
			AllowChangePassword = false,
			AllowUpdateProfile = true,
			AllowChangeEmail = false,
			AllowChangeUsername = true,
			AllowChangePhoneNumber = false,
			AllowDeleteAccount = true,
			EmailAsUsername = true,
			LoginWithEmail = true,
			AllowMultipleEmails = false,
			AllowDuplicateEmail = false,
			VerifyEmail = true,
			AllowFictitiousEmail = true,
			FictitiousEmailPattern = "{subjectId}@example.invalid",
			FictitiousEmailIsVerifiedByDefault = true,
			AllowProvidedSubjectId = true
		};
		source.PasswordOptions.MinimumLength = 14;
		source.PasswordOptions.PasswordReuseWindowDays = 90;
		source.PasswordOptions.MaxPasswordHistoryComparisons = 12;
		source.PasswordOptions.DisallowedWordsInPassword.Add("source-word");
		source.SecurityLifecycle.EnableSessionInvalidationByState = true;
		source.SecurityLifecycle.OnVoluntaryPasswordChange = SessionInvalidationPresets.RevokeOtherSessions;
		source.SecurityLifecycle.AuditCategories = SecurityAuditCategories.Credential;
		source.FixedFieldClaimProjections[0].ClaimType = "source_username";

		var copy = new UserAccountsRealmOptions(source);

		source.AllowRegistration = false;
		source.PasswordOptions.MinimumLength = 6;
		source.PasswordOptions.PasswordReuseWindowDays = 0;
		source.PasswordOptions.DisallowedWordsInPassword.Add("mutated-word");
		source.SecurityLifecycle.EnableSessionInvalidationByState = false;
		source.SecurityLifecycle.AuditCategories = SecurityAuditCategories.None;
		source.FixedFieldClaimProjections[0].ClaimType = "mutated_username";

		Assert.True(copy.AllowRegistration);
		Assert.False(copy.AllowForgotPassword);
		Assert.False(copy.AllowChangePassword);
		Assert.True(copy.AllowUpdateProfile);
		Assert.False(copy.AllowChangeEmail);
		Assert.True(copy.AllowChangeUsername);
		Assert.False(copy.AllowChangePhoneNumber);
		Assert.True(copy.AllowDeleteAccount);
		Assert.True(copy.EmailAsUsername);
		Assert.True(copy.LoginWithEmail);
		Assert.False(copy.AllowMultipleEmails);
		Assert.False(copy.AllowDuplicateEmail);
		Assert.True(copy.VerifyEmail);
		Assert.True(copy.AllowFictitiousEmail);
		Assert.Equal("{subjectId}@example.invalid", copy.FictitiousEmailPattern);
		Assert.True(copy.FictitiousEmailIsVerifiedByDefault);
		Assert.True(copy.AllowProvidedSubjectId);
		Assert.Equal(14, copy.PasswordOptions.MinimumLength);
		Assert.Equal(90, copy.PasswordOptions.PasswordReuseWindowDays);
		Assert.Equal(12, copy.PasswordOptions.MaxPasswordHistoryComparisons);
		Assert.Contains("source-word", copy.PasswordOptions.DisallowedWordsInPassword);
		Assert.DoesNotContain("mutated-word", copy.PasswordOptions.DisallowedWordsInPassword);
		Assert.True(copy.SecurityLifecycle.EnableSessionInvalidationByState);
		Assert.Equal(SessionInvalidationPresets.RevokeOtherSessions, copy.SecurityLifecycle.OnVoluntaryPasswordChange);
		Assert.Equal(SecurityAuditCategories.Credential, copy.SecurityLifecycle.AuditCategories);
		Assert.Equal("source_username", copy.FixedFieldClaimProjections[0].ClaimType);

		Assert.NotSame(source.PasswordOptions, copy.PasswordOptions);
		Assert.NotSame(source.PasswordOptions.DisallowedWordsInPassword, copy.PasswordOptions.DisallowedWordsInPassword);
		Assert.NotSame(source.SecurityLifecycle, copy.SecurityLifecycle);
		Assert.NotSame(source.FixedFieldClaimProjections, copy.FixedFieldClaimProjections);
		Assert.NotSame(source.FixedFieldClaimProjections[0], copy.FixedFieldClaimProjections[0]);
	}

	[Fact]
	public void SecurityLifecycle_Defaults_MatchDecidedPolicy()
	{
		var options = new UserAccountsRealmOptions();
		var lifecycle = options.SecurityLifecycle;

		// Per-trigger invalidation defaults (ADR-017 §2.7).
		Assert.Equal(SessionInvalidation.None, lifecycle.OnVoluntaryPasswordChange);
		Assert.Equal(SessionInvalidationPresets.RevokeAllSessionsAndRefreshTokens, lifecycle.OnPasswordRecoveryReset);
		Assert.Equal(SessionInvalidationPresets.RevokeAllSessionsAndRefreshTokens, lifecycle.OnAdminPasswordReset);
		Assert.Equal(SessionInvalidation.None, lifecycle.OnAdminMustChangePassword);
		Assert.Equal(SessionInvalidation.None, lifecycle.OnSensitiveProfileChange);

		// Opt-in policies are off by default (preserve current behavior).
		Assert.False(lifecycle.EnableSessionInvalidationByState);
		Assert.False(lifecycle.EnableSsoSessionExpiration);
		Assert.False(lifecycle.RequiresSecurityStateProvider);

		// Security audit categories on by default.
		Assert.Equal(SecurityAuditCategories.All, lifecycle.AuditCategories);
	}

	[Fact]
	public void SecurityLifecycle_RequiresSecurityStateProvider_FollowsStateInvalidation()
	{
		var options = new UserAccountsRealmOptions
		{
			SecurityLifecycle = { EnableSessionInvalidationByState = true }
		};

		Assert.True(options.SecurityLifecycle.RequiresSecurityStateProvider);
	}

	[Fact]
	public void Validate_Rejects_SsoIdleGreaterThanMax_WhenExpirationEnabled()
	{
		var options = new UserAccountsRealmOptions
		{
			SecurityLifecycle =
			{
				EnableSsoSessionExpiration = true,
				SsoSessionMaxMinutes = 60,
				SsoSessionIdleMinutes = 120
			}
		};

		var errors = options.Validate();

		Assert.Contains(errors, e => e.Contains("SsoSessionIdleMinutes", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Rejects_HistoryComparisonsCapBelowCount_WhenHistoryEnforced()
	{
		var options = new UserAccountsRealmOptions
		{
			PasswordOptions =
			{
				EnforcePasswordHistory = true,
				PasswordHistoryCount = 5,
				MaxPasswordHistoryComparisons = 3
			}
		};

		var errors = options.Validate();

		Assert.Contains(errors, e => e.Contains("MaxPasswordHistoryComparisons", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Allows_DefaultSecurityLifecycle()
	{
		var options = new UserAccountsRealmOptions();

		var errors = options.Validate();

		Assert.DoesNotContain(errors, e => e.Contains("SecurityLifecycle", StringComparison.Ordinal));
		Assert.DoesNotContain(errors, e => e.Contains("MaxPasswordHistoryComparisons", StringComparison.Ordinal));
	}

	[Fact]
	public void Defaults_ProjectExpectedFixedFields()
	{
		var options = new UserAccountsRealmOptions();

		Assert.Contains(options.FixedFieldClaimProjections, p =>
			p.Field == FixedAccountField.Username &&
			p.ScopeName == "profile" &&
			p.ClaimType == "preferred_username" &&
			p.Include);
		Assert.Contains(options.FixedFieldClaimProjections, p =>
			p.Field == FixedAccountField.PrimaryEmail &&
			p.ScopeName == "email" &&
			p.ClaimType == "email" &&
			p.Include);
		Assert.Contains(options.FixedFieldClaimProjections, p =>
			p.Field == FixedAccountField.ExternalId &&
			p.ScopeName == "profile" &&
			p.ClaimType == "external_id" &&
			!p.Include);
	}

	[Fact]
	public void Validate_Rejects_EmailLogin_WithDuplicateEmail()
	{
		var options = new UserAccountsRealmOptions
		{
			LoginWithEmail = true,
			AllowDuplicateEmail = true
		};

		var errors = options.Validate();

		Assert.Contains(errors, e => e.Contains("Email login", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Rejects_EmailAsUsername_WithDuplicateEmail()
	{
		var options = new UserAccountsRealmOptions
		{
			EmailAsUsername = true,
			AllowDuplicateEmail = true
		};

		var errors = options.Validate();

		Assert.Contains(errors, e => e.Contains("Email login", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Rejects_FictitiousPatternWithoutSubjectToken_WhenDuplicateEmailDisallowed()
	{
		var options = new UserAccountsRealmOptions
		{
			AllowFictitiousEmail = true,
			AllowDuplicateEmail = false,
			FictitiousEmailPattern = "user@fictitious.local"
		};

		var errors = options.Validate();

		Assert.Contains(errors, e => e.Contains("FictitiousEmailPattern", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Allows_FictitiousPatternWithSubjectToken_WhenDuplicateEmailDisallowed()
	{
		var options = new UserAccountsRealmOptions
		{
			AllowFictitiousEmail = true,
			AllowDuplicateEmail = false,
			FictitiousEmailPattern = "{subjectId}@fictitious.local"
		};

		var errors = options.Validate();

		Assert.DoesNotContain(errors, e => e.Contains("FictitiousEmailPattern", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Allows_FictitiousPatternWithoutSubjectToken_WhenDuplicateEmailAllowed()
	{
		var options = new UserAccountsRealmOptions
		{
			AllowFictitiousEmail = true,
			AllowDuplicateEmail = true,
			FictitiousEmailPattern = "user@fictitious.local"
		};

		var errors = options.Validate();

		Assert.DoesNotContain(errors, e => e.Contains("FictitiousEmailPattern", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Rejects_DuplicateFixedClaimType()
	{
		var options = new UserAccountsRealmOptions();
		options.FixedFieldClaimProjections.Add(new FixedFieldClaimProjection
		{
			Field = FixedAccountField.ExternalId,
			ScopeName = "profile",
			ClaimType = "name"
		});

		var errors = options.Validate();

		Assert.Contains(errors, e => e.Contains("'name'", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_Rejects_DynamicClaimCollision()
	{
		var options = new UserAccountsRealmOptions();

		var errors = options.Validate(["email"]);

		Assert.Contains(errors, e => e.Contains("'email'", StringComparison.Ordinal));
	}
}
