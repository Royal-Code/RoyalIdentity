namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// Minimal account policy used only by the in-memory fake for IdP tests.
/// </summary>
public sealed class MemoryAccountOptions
{
	/// <summary>
	/// Gets or sets whether the email address is also the username.
	/// </summary>
	public bool EmailAsUsername { get; set; } = false;

	/// <summary>
	/// Gets or sets whether local login can resolve a verified primary email address.
	/// </summary>
	public bool LoginWithEmail { get; set; } = false;

	/// <summary>
	/// Gets or sets whether the same email address may be used by multiple accounts in the same realm.
	/// </summary>
	public bool AllowDuplicateEmail { get; set; } = false;

	/// <summary>
	/// Gets password and lockout policies used by the fake authenticator.
	/// </summary>
	public MemoryPasswordOptions PasswordOptions { get; } = new();
}

