namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>
/// Versioned envelope produced by a signing-key material protector. The persisted representation keeps the
/// protector identifier in its own relational column and prefixes the opaque payload with its envelope
/// version. <see cref="ToString"/> deliberately omits the payload.
/// </summary>
public sealed class KeyMaterialEnvelope
{
	public const int CurrentVersion = 1;

	public KeyMaterialEnvelope(string protectorId, string payload, int version = CurrentVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(protectorId);
		ArgumentException.ThrowIfNullOrEmpty(payload);

		ProtectorId = protectorId;
		Payload = payload;
		Version = version;
	}

	public string ProtectorId { get; }

	public int Version { get; }

	public string Payload { get; }

	public string ToPersistedPayload() => $"v{Version}:{Payload}";

	public static KeyMaterialEnvelope Parse(string protectorId, string persistedPayload)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(protectorId);
		ArgumentException.ThrowIfNullOrEmpty(persistedPayload);

		var separator = persistedPayload.IndexOf(':', StringComparison.Ordinal);
		if (separator < 2
			|| persistedPayload[0] != 'v'
			|| !int.TryParse(persistedPayload.AsSpan(1, separator - 1), out var version)
			|| version != CurrentVersion
			|| separator == persistedPayload.Length - 1)
		{
			throw new InvalidOperationException("The persisted signing-key material envelope is invalid or unsupported.");
		}

		return new KeyMaterialEnvelope(protectorId, persistedPayload[(separator + 1)..], version);
	}

	public override string ToString()
		=> $"KeyMaterialEnvelope {{ ProtectorId = {ProtectorId}, Version = {Version}, Payload = [REDACTED] }}";
}
