namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Lifecycle status for a versioned property scope definition.
/// </summary>
public enum PropertyScopeVersionStatus
{
	/// <summary>The version is editable and not yet submitted.</summary>
	Draft,

	/// <summary>The version is waiting for administrative approval.</summary>
	PendingApproval,

	/// <summary>The version is the one used for claim projection and property writes.</summary>
	Active,

	/// <summary>The version was previously active and is now historical.</summary>
	Archived,

	/// <summary>The version was rejected and will not become active.</summary>
	Rejected
}
