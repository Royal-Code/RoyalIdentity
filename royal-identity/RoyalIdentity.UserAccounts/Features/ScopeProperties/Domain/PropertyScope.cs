using RoyalCode.Aggregates;
using RoyalCode.SmartProblems;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Realm-scoped dynamic property scope that maps to an identity scope name.
/// </summary>
public class PropertyScope : AggregateRoot<long>
{
	private List<PropertyDefinition> definitions = [];
	private List<PropertyScopeVersion> versions = [];

#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyScope()
	{
	}
#nullable restore

	/// <summary>
	/// Creates a property scope with an initial draft version.
	/// </summary>
	/// <param name="realmId">The owning realm.</param>
	/// <param name="name">The identity scope name.</param>
	/// <param name="displayName">The initial display name.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	public PropertyScope(string realmId, string name, string? displayName, DateTimeOffset createdAt)
	{
		RealmId = realmId;
		Name = name;
		IsActive = true;

		var version = new PropertyScopeVersion(RealmId, this, 1, displayName, createdAt);
		VersionItems.Add(version);
	}

	/// <summary>
	/// Gets the realm that owns this property scope.
	/// </summary>
	public string RealmId { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the identity scope name represented by this property scope.
	/// </summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>
	/// Gets whether this property scope participates in writes and projection.
	/// </summary>
	public bool IsActive { get; private set; }

	/// <summary>
	/// Gets the active version foreign key when persisted.
	/// </summary>
	public long? ActiveVersionId { get; private set; }

	/// <summary>
	/// Gets the active version.
	/// </summary>
	public PropertyScopeVersion? ActiveVersion =>
		VersionItems.FirstOrDefault(r => r.Status is PropertyScopeVersionStatus.Active);

	/// <summary>
	/// Gets all versions.
	/// </summary>
	public IReadOnlyCollection<PropertyScopeVersion> Versions => versions;

	/// <summary>
	/// Gets all stable property definitions.
	/// </summary>
	public IReadOnlyCollection<PropertyDefinition> Definitions => definitions;

	/// <summary>
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<PropertyScopeVersion> VersionItems
	{
		get => versions;
		set => versions = value;
	}

	/// <summary>
	/// Collection navigation used by EF Core mapping while public access remains read-only.
	/// </summary>
	protected virtual List<PropertyDefinition> DefinitionItems
	{
		get => definitions;
		set => definitions = value;
	}

	/// <summary>
	/// Creates a new draft version without changing the active version.
	/// </summary>
	/// <param name="displayName">The display name for the new version.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <returns>A result describing whether a draft was created.</returns>
	public Result CreateDraftVersion(string? displayName, DateTimeOffset createdAt)
	{
		if (VersionItems.Any(r => r.Status is PropertyScopeVersionStatus.Draft or PropertyScopeVersionStatus.PendingApproval))
		{
			return Problems.InvalidState("A draft or pending version already exists.", nameof(Versions), "user_account.property_scope_version_open");
		}

		var nextVersionNumber = VersionItems.Count is 0
			? 1
			: VersionItems.Max(r => r.Version) + 1;
		var draftVersion = new PropertyScopeVersion(RealmId, this, nextVersionNumber, displayName, createdAt);

		var activeVersion = ActiveVersion;
		if (activeVersion is not null)
		{
			foreach (var definitionVersion in activeVersion.DefinitionVersions)
			{
				draftVersion.AddDefinitionVersion(definitionVersion.CopyTo(draftVersion));
			}
		}

		VersionItems.Add(draftVersion);
		return Result.Ok();
	}

	/// <summary>
	/// Adds a property definition to a scope version.
	/// </summary>
	/// <param name="version">The target version.</param>
	/// <param name="claimType">The claim type.</param>
	/// <param name="settings">The versioned definition settings.</param>
	/// <returns>A result describing whether the definition was added.</returns>
	public Result AddDefinition(
		PropertyScopeVersion version,
		string claimType,
		PropertyDefinitionSettings settings)
	{
		var versionResult = EnsureVersionCanBeEdited(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		if (version.DefinitionVersions.Any(d => d.ClaimType == claimType))
		{
			return Problems.InvalidState("Property definition already exists in this version.", nameof(claimType), "user_account.property_definition_duplicate");
		}

		var definition = DefinitionItems.FirstOrDefault(d => d.ClaimType == claimType);
		if (definition is null)
		{
			definition = new PropertyDefinition(RealmId, this, claimType);
			DefinitionItems.Add(definition);
		}

		var definitionVersion = new PropertyDefinitionVersion(RealmId, version, definition, settings);
		version.AddDefinitionVersion(definitionVersion);
		AddEvent(new PropertyDefinitionChanged(RealmId, Name, claimType));
		return Result.Ok();
	}

	/// <summary>
	/// Submits a draft version for approval.
	/// </summary>
	/// <param name="version">The version to submit.</param>
	/// <returns>A result describing whether the version was submitted.</returns>
	public Result SubmitVersionForApproval(PropertyScopeVersion version)
	{
		var versionResult = EnsureVersionBelongsToScope(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		if (version.Status is not PropertyScopeVersionStatus.Draft)
		{
			return Problems.InvalidState("Only draft versions can be submitted.", nameof(version), "user_account.property_scope_version_not_submittable");
		}

		version.SubmitForApproval();
		return Result.Ok();
	}

	/// <summary>
	/// Rejects a draft or pending version.
	/// </summary>
	/// <param name="version">The version to reject.</param>
	/// <returns>A result describing whether the version was rejected.</returns>
	public Result RejectVersion(PropertyScopeVersion version)
	{
		var versionResult = EnsureVersionBelongsToScope(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		if (version.Status is not (PropertyScopeVersionStatus.Draft or PropertyScopeVersionStatus.PendingApproval))
		{
			return Problems.InvalidState("Only draft or pending versions can be rejected.", nameof(version), "user_account.property_scope_version_not_rejectable");
		}

		version.Reject();
		return Result.Ok();
	}

	/// <summary>
	/// Updates a property definition inside an editable scope version.
	/// </summary>
	/// <param name="version">The version that owns the definition version.</param>
	/// <param name="claimType">The claim type to update.</param>
	/// <param name="settings">The updated definition settings.</param>
	/// <returns>A result describing whether the definition was updated.</returns>
	public Result UpdateDefinition(
		PropertyScopeVersion version,
		string claimType,
		PropertyDefinitionSettings settings)
	{
		var versionResult = EnsureVersionCanBeEdited(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		var definitionVersion = version.DefinitionVersions.FirstOrDefault(d => d.ClaimType == claimType);
		if (definitionVersion is null)
		{
			return Problems.InvalidState("Property definition does not exist in this version.", nameof(claimType), "user_account.property_definition_missing");
		}

		definitionVersion.Update(settings);
		AddEvent(new PropertyDefinitionChanged(RealmId, Name, claimType));
		return Result.Ok();
	}

	/// <summary>
	/// Marks a property definition as active inside an editable scope version.
	/// </summary>
	/// <param name="version">The version that owns the definition version.</param>
	/// <param name="claimType">The claim type to activate.</param>
	/// <returns>A result describing whether the definition was activated.</returns>
	public Result ActivateDefinition(PropertyScopeVersion version, string claimType)
	{
		var versionResult = EnsureVersionCanBeEdited(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		var definitionVersion = version.DefinitionVersions.FirstOrDefault(d => d.ClaimType == claimType);
		if (definitionVersion is null)
		{
			return Problems.InvalidState("Property definition does not exist in this version.", nameof(claimType), "user_account.property_definition_missing");
		}

		definitionVersion.Activate();
		AddEvent(new PropertyDefinitionChanged(RealmId, Name, claimType));
		return Result.Ok();
	}

	/// <summary>
	/// Marks a property definition as inactive inside an editable scope version.
	/// </summary>
	/// <param name="version">The version that owns the definition version.</param>
	/// <param name="claimType">The claim type to deactivate.</param>
	/// <returns>A result describing whether the definition was deactivated.</returns>
	public Result DeactivateDefinition(PropertyScopeVersion version, string claimType)
	{
		var versionResult = EnsureVersionCanBeEdited(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		var definitionVersion = version.DefinitionVersions.FirstOrDefault(d => d.ClaimType == claimType);
		if (definitionVersion is null)
		{
			return Problems.InvalidState("Property definition does not exist in this version.", nameof(claimType), "user_account.property_definition_missing");
		}

		definitionVersion.Deactivate();
		AddEvent(new PropertyDefinitionChanged(RealmId, Name, claimType));
		return Result.Ok();
	}

	/// <summary>
	/// Approves a version and makes it active.
	/// </summary>
	/// <param name="version">The version to activate.</param>
	/// <param name="approvedAt">The approval timestamp.</param>
	/// <returns>A result describing whether the version was activated.</returns>
	public Result ApproveVersion(PropertyScopeVersion version, DateTimeOffset approvedAt)
	{
		var versionResult = EnsureVersionBelongsToScope(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		if (version.Status is not (PropertyScopeVersionStatus.Draft or PropertyScopeVersionStatus.PendingApproval))
		{
			return Problems.InvalidState("Only draft or pending versions can be approved.", nameof(version), "user_account.property_scope_version_not_approvable");
		}

		foreach (var activeVersion in VersionItems.Where(r => r.Status is PropertyScopeVersionStatus.Active))
		{
			activeVersion.Archive();
		}

		version.MarkActive(approvedAt);
		ActiveVersionId = version.Id == default ? null : version.Id;
		AddEvent(new PropertyScopeVersionActivated(RealmId, Name, version.Version));
		return Result.Ok();
	}

	/// <summary>
	/// Marks the scope as active.
	/// </summary>
	public void Activate()
	{
		IsActive = true;
	}

	/// <summary>
	/// Marks the scope as inactive without deleting definitions or values.
	/// </summary>
	public void Deactivate()
	{
		IsActive = false;
	}

	private Result EnsureVersionCanBeEdited(PropertyScopeVersion version)
	{
		var versionResult = EnsureVersionBelongsToScope(version);
		if (versionResult.IsFailure)
		{
			return versionResult;
		}

		if (version.Status is not PropertyScopeVersionStatus.Draft)
		{
			return Problems.InvalidState("Only draft versions can be edited.", nameof(version), "user_account.property_scope_version_not_editable");
		}

		return Result.Ok();
	}

	private Result EnsureVersionBelongsToScope(PropertyScopeVersion version)
	{
		if (version.PropertyScope != this)
		{
			return Problems.InvalidState("Property scope version does not belong to this scope.", nameof(version), "user_account.property_scope_version_mismatch");
		}

		return Result.Ok();
	}
}
