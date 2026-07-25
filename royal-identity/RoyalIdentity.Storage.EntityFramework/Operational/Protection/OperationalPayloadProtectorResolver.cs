namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Registry of the operational payload protection profiles the composition registered (plan DF30). Writes
/// resolve the profile a realm selected by id; reads resolve the profile recorded in the envelope, so a
/// rotation only requires the previous profile to stay registered. Every miss fails closed — there is no
/// implicit default profile and never a fallback to <see cref="PlainOperationalPayloadProtector"/>.
/// </summary>
public sealed class OperationalPayloadProtectorResolver
{
	private readonly IReadOnlyDictionary<string, IOperationalPayloadProtector> profiles;

	public OperationalPayloadProtectorResolver(IEnumerable<IOperationalPayloadProtector> protectors)
	{
		ArgumentNullException.ThrowIfNull(protectors);

		var byId = new Dictionary<string, IOperationalPayloadProtector>(StringComparer.Ordinal);
		foreach (var protector in protectors)
		{
			if (!byId.TryAdd(protector.ProfileId, protector))
				throw OperationalPayloadProtectionException.DuplicateProfile(protector.ProfileId);
		}

		profiles = byId;
	}

	/// <summary>The profile a realm writes with, by the id it selected.</summary>
	public IOperationalPayloadProtector GetForWrite(string profileId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

		return profiles.TryGetValue(profileId, out var protector)
			? protector
			: throw OperationalPayloadProtectionException.ProfileNotRegistered(profileId);
	}

	/// <summary>The profile recorded in a persisted envelope.</summary>
	public IOperationalPayloadProtector GetForRead(string protectorId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(protectorId);

		return profiles.TryGetValue(protectorId, out var protector)
			? protector
			: throw OperationalPayloadProtectionException.ReaderNotRegistered(protectorId);
	}
}
