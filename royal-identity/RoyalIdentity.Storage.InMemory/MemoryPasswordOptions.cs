namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// Minimal password and lockout policy used only by the in-memory fake for IdP tests.
/// </summary>
public sealed class MemoryPasswordOptions
{
	/// <summary>
	/// Gets or sets the maximum failed attempts before lockout. Zero disables lockout.
	/// </summary>
	public int MaxFailedAccessAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the lockout duration in minutes. Zero keeps the account locked until reset.
	/// </summary>
	public int AccountLockoutDurationMinutes { get; set; } = 30;
}

