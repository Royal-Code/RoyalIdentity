using Microsoft.Extensions.Logging;

namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>
/// Explicit development/operator opt-in that stores signing-key material without encryption. Never
/// registered by the Configuration storage defaults.
/// </summary>
public sealed class PlainKeyMaterialProtector : IKeyMaterialProtector
{
	public const string Id = "plain";

	public PlainKeyMaterialProtector(ILogger<PlainKeyMaterialProtector> logger)
	{
		logger.LogWarning(
			"Plain signing-key material protection is active. Persisted signing keys are not encrypted at rest.");
	}

	public string ProtectorId => Id;

	public ValueTask<KeyMaterialEnvelope> ProtectAsync(string material, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(material);
		ct.ThrowIfCancellationRequested();
		return ValueTask.FromResult(new KeyMaterialEnvelope(ProtectorId, material));
	}

	public ValueTask<string> UnprotectAsync(KeyMaterialEnvelope envelope, CancellationToken ct = default)
	{
		ValidateEnvelope(envelope);
		ct.ThrowIfCancellationRequested();
		return ValueTask.FromResult(envelope.Payload);
	}

	private static void ValidateEnvelope(KeyMaterialEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		if (envelope.Version != KeyMaterialEnvelope.CurrentVersion
			|| !string.Equals(envelope.ProtectorId, Id, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The signing-key material envelope is incompatible with the Plain protector.");
		}
	}
}
