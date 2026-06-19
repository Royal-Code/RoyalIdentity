using RoyalCode.DomainEvents;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Event raised when a user account is created.
/// </summary>
public class UserAccountCreated(string realmId, string subjectId, string username) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the account username.
	/// </summary>
	public string Username { get; } = username;
}

/// <summary>
/// Event raised when the username changes.
/// </summary>
public class UserAccountUsernameChanged(string realmId, string subjectId, string username) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the new username.
	/// </summary>
	public string Username { get; } = username;
}

/// <summary>
/// Event raised when an email is added.
/// </summary>
public class UserAccountEmailAdded(string realmId, string subjectId, string address, bool isPrimary) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the email address.
	/// </summary>
	public string Address { get; } = address;

	/// <summary>
	/// Gets whether the email became primary.
	/// </summary>
	public bool IsPrimary { get; } = isPrimary;
}

/// <summary>
/// Event raised when the primary email changes.
/// </summary>
public class UserAccountPrimaryEmailChanged(string realmId, string subjectId, string address) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the new primary email address.
	/// </summary>
	public string Address { get; } = address;
}

/// <summary>
/// Event raised when a role is added.
/// </summary>
public class UserAccountRoleAdded(string realmId, string subjectId, string role) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the added role.
	/// </summary>
	public string Role { get; } = role;
}

/// <summary>
/// Event raised when a role is removed.
/// </summary>
public class UserAccountRoleRemoved(string realmId, string subjectId, string role) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the removed role.
	/// </summary>
	public string Role { get; } = role;
}

/// <summary>
/// Event raised when the password credential changes.
/// </summary>
public class UserAccountPasswordChanged(string realmId, string subjectId) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;
}

/// <summary>
/// Event raised when the local credential is locked after failed attempts.
/// </summary>
public class UserAccountLocalCredentialLocked(string realmId, string subjectId, DateTimeOffset? lockoutEndAt) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets when lockout ends, or <c>null</c> for administrative unlock.
	/// </summary>
	public DateTimeOffset? LockoutEndAt { get; } = lockoutEndAt;
}

/// <summary>
/// Event raised when the account is activated.
/// </summary>
public class UserAccountActivated(string realmId, string subjectId) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;
}

/// <summary>
/// Event raised when the account is deactivated.
/// </summary>
public class UserAccountDeactivated(string realmId, string subjectId) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;
}

/// <summary>
/// Event raised when the account is administratively blocked.
/// </summary>
public class UserAccountBlocked(string realmId, string subjectId, string? reason) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;

	/// <summary>
	/// Gets the block reason.
	/// </summary>
	public string? Reason { get; } = reason;
}

/// <summary>
/// Event raised when the account administrative block is cleared.
/// </summary>
public class UserAccountUnblocked(string realmId, string subjectId) : DomainEventBase
{
	/// <summary>
	/// Gets the account realm.
	/// </summary>
	public string RealmId { get; } = realmId;

	/// <summary>
	/// Gets the immutable subject identifier.
	/// </summary>
	public string SubjectId { get; } = subjectId;
}
