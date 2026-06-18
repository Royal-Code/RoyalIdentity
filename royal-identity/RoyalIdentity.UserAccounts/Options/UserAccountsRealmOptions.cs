namespace RoyalIdentity.UserAccounts.Options;

/// <summary>
/// Realm-scoped policies owned by the UserAccounts module.
/// </summary>
public class UserAccountsRealmOptions
{
	/// <summary>
	/// Creates a new instance with default policies and default fixed-field claim projections.
	/// </summary>
	public UserAccountsRealmOptions()
	{
		FixedFieldClaimProjections = CreateDefaultFixedFieldClaimProjections();
	}

	/// <summary>
	/// Creates an independent copy of another instance.
	/// </summary>
	public UserAccountsRealmOptions(UserAccountsRealmOptions other)
	{
		AllowRegistration = other.AllowRegistration;
		AllowForgotPassword = other.AllowForgotPassword;
		AllowChangePassword = other.AllowChangePassword;
		AllowUpdateProfile = other.AllowUpdateProfile;
		AllowChangeEmail = other.AllowChangeEmail;
		AllowChangeUsername = other.AllowChangeUsername;
		AllowChangePhoneNumber = other.AllowChangePhoneNumber;
		AllowDeleteAccount = other.AllowDeleteAccount;
		EmailAsUsername = other.EmailAsUsername;
		LoginWithEmail = other.LoginWithEmail;
		AllowMultipleEmails = other.AllowMultipleEmails;
		AllowDuplicateEmail = other.AllowDuplicateEmail;
		VerifyEmail = other.VerifyEmail;
		AllowFictitiousEmail = other.AllowFictitiousEmail;
		FictitiousEmailPattern = other.FictitiousEmailPattern;
		FictitiousEmailIsVerifiedByDefault = other.FictitiousEmailIsVerifiedByDefault;
		AllowProvidedSubjectId = other.AllowProvidedSubjectId;
		PasswordOptions = new PasswordOptions(other.PasswordOptions);
		FixedFieldClaimProjections = other.FixedFieldClaimProjections
			.Select(p => new FixedFieldClaimProjection(p))
			.ToList();
	}

	/// <summary>
	/// Gets or sets whether self-registration is enabled.
	/// </summary>
	public bool AllowRegistration { get; set; } = false;

	/// <summary>
	/// Gets or sets whether password recovery is enabled.
	/// </summary>
	public bool AllowForgotPassword { get; set; } = true;

	/// <summary>
	/// Gets or sets whether users can change their password.
	/// </summary>
	public bool AllowChangePassword { get; set; } = true;

	/// <summary>
	/// Gets or sets whether users can update their profile.
	/// </summary>
	public bool AllowUpdateProfile { get; set; } = false;

	/// <summary>
	/// Gets or sets whether users can change their email address.
	/// </summary>
	public bool AllowChangeEmail { get; set; } = true;

	/// <summary>
	/// Gets or sets whether users can change their username.
	/// </summary>
	public bool AllowChangeUsername { get; set; } = false;

	/// <summary>
	/// Gets or sets whether users can change their phone number.
	/// </summary>
	public bool AllowChangePhoneNumber { get; set; } = true;

	/// <summary>
	/// Gets or sets whether users can delete their account.
	/// </summary>
	public bool AllowDeleteAccount { get; set; } = false;

	/// <summary>
	/// Gets or sets whether the email address is also the username.
	/// </summary>
	public bool EmailAsUsername { get; set; } = false;

	/// <summary>
	/// Gets or sets whether local login can resolve a verified primary email address.
	/// </summary>
	public bool LoginWithEmail { get; set; } = false;

	/// <summary>
	/// Gets or sets whether an account can hold more than one email address.
	/// </summary>
	public bool AllowMultipleEmails { get; set; } = true;

	/// <summary>
	/// Gets or sets whether the same email address may be used by multiple accounts in the same realm.
	/// </summary>
	public bool AllowDuplicateEmail { get; set; } = false;

	/// <summary>
	/// Gets or sets whether email verification is required by account policies.
	/// </summary>
	public bool VerifyEmail { get; set; } = false;

	/// <summary>
	/// Gets or sets whether the module may generate fictitious email addresses for accounts without a real email.
	/// </summary>
	public bool AllowFictitiousEmail { get; set; } = false;

	/// <summary>
	/// Gets or sets the pattern used to create fictitious email addresses.
	/// </summary>
	public string FictitiousEmailPattern { get; set; } = "{subjectId}@fictitious.local";

	/// <summary>
	/// Gets or sets whether fictitious emails are marked verified by default.
	/// </summary>
	public bool FictitiousEmailIsVerifiedByDefault { get; set; } = false;

	/// <summary>
	/// Gets or sets whether a caller may provide the OIDC subject identifier when creating an account.
	/// </summary>
	public bool AllowProvidedSubjectId { get; set; } = false;

	/// <summary>
	/// Gets or sets password and lockout policies.
	/// </summary>
	public PasswordOptions PasswordOptions { get; set; } = new();

	/// <summary>
	/// Gets fixed account field projections for identity-scope claims.
	/// </summary>
	public List<FixedFieldClaimProjection> FixedFieldClaimProjections { get; set; }

	/// <summary>
	/// Validates option combinations and claim projection uniqueness.
	/// </summary>
	/// <param name="dynamicClaimTypes">Optional dynamic property claim types to check against fixed projections.</param>
	/// <returns>A list of configuration errors. Empty means valid.</returns>
	public IReadOnlyList<string> Validate(IEnumerable<string>? dynamicClaimTypes = null)
	{
		List<string> errors = [];

		if ((EmailAsUsername || LoginWithEmail) && AllowDuplicateEmail)
		{
			errors.Add("Email login requires AllowDuplicateEmail to be false.");
		}

		if (PasswordOptions.MinimumLength > PasswordOptions.MaximumLength)
		{
			errors.Add("PasswordOptions.MinimumLength cannot be greater than PasswordOptions.MaximumLength.");
		}

		foreach (var projection in FixedFieldClaimProjections.Where(p => p.Include))
		{
			if (string.IsNullOrWhiteSpace(projection.ScopeName))
			{
				errors.Add($"Fixed field projection '{projection.Field}' must define a scope name.");
			}

			if (string.IsNullOrWhiteSpace(projection.ClaimType))
			{
				errors.Add($"Fixed field projection '{projection.Field}' must define a claim type.");
			}
		}

		var claimTypes = FixedFieldClaimProjections
			.Where(p => p.Include && !string.IsNullOrWhiteSpace(p.ClaimType))
			.Select(p => p.ClaimType);

		if (dynamicClaimTypes is not null)
		{
			claimTypes = claimTypes.Concat(dynamicClaimTypes.Where(c => !string.IsNullOrWhiteSpace(c)));
		}

		var duplicateClaimTypes = claimTypes
			.GroupBy(c => c, StringComparer.Ordinal)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.OrderBy(c => c, StringComparer.Ordinal);

		foreach (var duplicate in duplicateClaimTypes)
		{
			errors.Add($"Claim type '{duplicate}' is configured more than once in this realm.");
		}

		return errors;
	}

	/// <summary>
	/// Throws when option combinations are invalid.
	/// </summary>
	public void EnsureValid(IEnumerable<string>? dynamicClaimTypes = null)
	{
		var errors = Validate(dynamicClaimTypes);
		if (errors.Count is 0)
			return;

		throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
	}

	private static List<FixedFieldClaimProjection> CreateDefaultFixedFieldClaimProjections()
	{
		return
		[
			new()
			{
				Field = FixedAccountField.Username,
				ScopeName = "profile",
				ClaimType = "preferred_username"
			},
			new()
			{
				Field = FixedAccountField.DisplayName,
				ScopeName = "profile",
				ClaimType = "name"
			},
			new()
			{
				Field = FixedAccountField.PrimaryEmail,
				ScopeName = "email",
				ClaimType = "email"
			},
			new()
			{
				Field = FixedAccountField.EmailVerified,
				ScopeName = "email",
				ClaimType = "email_verified"
			},
			new()
			{
				Field = FixedAccountField.Roles,
				ScopeName = "profile",
				ClaimType = "role"
			},
			new()
			{
				Field = FixedAccountField.ExternalId,
				ScopeName = "profile",
				ClaimType = "external_id",
				Include = false
			}
		];
	}
}
