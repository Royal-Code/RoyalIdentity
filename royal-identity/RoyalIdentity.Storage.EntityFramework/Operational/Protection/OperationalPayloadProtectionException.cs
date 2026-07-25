namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Raised when an operational payload cannot be protected or restored (plan DF30). Every case fails closed:
/// nothing here ever falls back to storing or reading an unprotected value. Messages never include payloads,
/// handles or key material (plan DF28).
/// </summary>
public sealed class OperationalPayloadProtectionException : Exception
{
	private OperationalPayloadProtectionException(string message) : base(message)
	{
	}

	private OperationalPayloadProtectionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>The realm selects a profile that the composition did not register.</summary>
	public static OperationalPayloadProtectionException ProfileNotRegistered(string profileId)
		=> new($"The operational payload protection profile '{profileId}' is not registered.");

	/// <summary>More than one profile claims the same id.</summary>
	public static OperationalPayloadProtectionException DuplicateProfile(string profileId)
		=> new($"More than one operational payload protection profile is registered with the id '{profileId}'.");

	/// <summary>The persisted envelope is malformed or of an unsupported version.</summary>
	public static OperationalPayloadProtectionException InvalidEnvelope()
		=> new("The persisted operational payload envelope is invalid or unsupported.");

	/// <summary>The profile that wrote the record is no longer registered as a reader.</summary>
	public static OperationalPayloadProtectionException ReaderNotRegistered(string profileId)
		=> new($"The persisted operational payload requires the profile '{profileId}', which is not registered.");

	/// <summary>The payload could not be restored: tampered with, or produced under a different context.</summary>
	public static OperationalPayloadProtectionException Unreadable(string profileId, Exception innerException)
		=> new($"The persisted operational payload could not be restored by the profile '{profileId}'.", innerException);
}
