namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Raised when a persisted Configuration JSON payload cannot be turned back into a complete, trusted object:
/// an unknown payload version or malformed, structurally invalid or empty JSON (plan: "falha de payload/version é erro de configuração
/// e nunca retorna objeto parcial"). It is a fail-closed signal — the caller must abort, never fall back to a
/// partial graph. The message never carries the payload itself (plan invariante 10).
/// </summary>
public sealed class ConfigurationPayloadException : Exception
{
	private ConfigurationPayloadException(string message, Exception? inner = null) : base(message, inner)
	{
	}

	public static ConfigurationPayloadException UnsupportedVersion(string payloadName, int found, int expected)
		=> new($"The persisted {payloadName} payload has version {found}, but only version {expected} is supported.");

	public static ConfigurationPayloadException InvalidJson(string payloadName, Exception inner)
		=> new($"The persisted {payloadName} payload is not valid JSON.", inner);

	public static ConfigurationPayloadException EmptyPayload(string payloadName)
		=> new($"The persisted {payloadName} payload deserialized to null.");
}
