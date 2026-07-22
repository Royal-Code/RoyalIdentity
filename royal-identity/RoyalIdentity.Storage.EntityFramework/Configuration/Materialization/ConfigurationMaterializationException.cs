namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Raised when persisted relational Configuration rows cannot form one complete, realm-bound core object.
/// Messages identify only the invalid shape and never include client ids, realm ids, secrets or row values.
/// </summary>
public sealed class ConfigurationMaterializationException : Exception
{
	private ConfigurationMaterializationException(string message) : base(message)
	{
	}

	internal static ConfigurationMaterializationException RealmMismatch()
		=> new("The persisted client root does not belong to the requested realm.");

	internal static ConfigurationMaterializationException SatelliteMismatch(string satellite)
		=> new($"The persisted client {satellite} rows do not belong to the client root.");

	internal static ConfigurationMaterializationException UnknownStringValueKind()
		=> new("The persisted client contains an unsupported string-value kind.");

	internal static ConfigurationMaterializationException InvalidEnum(string property)
		=> new($"The persisted client contains an invalid {property} value.");
}
