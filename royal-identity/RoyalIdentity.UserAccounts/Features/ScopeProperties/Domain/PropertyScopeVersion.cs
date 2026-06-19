using RoyalCode.Entities;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Editable version of a property scope schema.
/// </summary>
public class PropertyScopeVersion : Entity<long>
{
	private List<PropertyDefinitionVersion> definitionVersions = [];

#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyScopeVersion()
	{
	}
#nullable restore

	internal PropertyScopeVersion(
		string realmId,
		PropertyScope propertyScope,
		int version,
		string? displayName,
		DateTimeOffset createdAt)
	{
		RealmId = realmId;
		PropertyScopeId = propertyScope.Id;
		PropertyScope = propertyScope;
		Version = version;
		Status = PropertyScopeVersionStatus.Draft;
		DisplayName = displayName;
		CreatedAt = createdAt;
	}

	/// <summary>
	/// Gets the realm that owns this scope version.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the owning scope foreign key.
	/// </summary>
	public long PropertyScopeId { get; private set; }

	/// <summary>
	/// Gets the owning property scope.
	/// </summary>
	public virtual PropertyScope? PropertyScope { get; private set; }

	/// <summary>
	/// Gets the schema version number.
	/// </summary>
	public int Version { get; private set; }

	/// <summary>
	/// Gets the version status.
	/// </summary>
	public PropertyScopeVersionStatus Status { get; private set; }

	/// <summary>
	/// Gets the display name for this version.
	/// </summary>
	public string? DisplayName { get; private set; }

	/// <summary>
	/// Gets when the version was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Gets when the version was approved.
	/// </summary>
	public DateTimeOffset? ApprovedAt { get; private set; }

	/// <summary>
	/// Gets definition versions for this scope version.
	/// </summary>
	public IReadOnlyCollection<PropertyDefinitionVersion> DefinitionVersions => definitionVersions;

	/// <summary>
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<PropertyDefinitionVersion> DefinitionVersionItems
	{
		get => definitionVersions;
		set => definitionVersions = value;
	}

	/// <summary>
	/// Submits this draft version for approval.
	/// </summary>
	internal void SubmitForApproval()
	{
		if (Status is PropertyScopeVersionStatus.Draft)
		{
			Status = PropertyScopeVersionStatus.PendingApproval;
		}
	}

	/// <summary>
	/// Rejects this version.
	/// </summary>
	internal void Reject()
	{
		if (Status is PropertyScopeVersionStatus.Draft or PropertyScopeVersionStatus.PendingApproval)
		{
			Status = PropertyScopeVersionStatus.Rejected;
		}
	}

	internal void AddDefinitionVersion(PropertyDefinitionVersion definitionVersion)
	{
		definitionVersion.AttachTo(this);
		DefinitionVersionItems.Add(definitionVersion);
	}

	internal void MarkActive(DateTimeOffset approvedAt)
	{
		Status = PropertyScopeVersionStatus.Active;
		ApprovedAt = approvedAt;
	}

	internal void Archive()
	{
		if (Status is PropertyScopeVersionStatus.Active)
		{
			Status = PropertyScopeVersionStatus.Archived;
		}
	}
}
