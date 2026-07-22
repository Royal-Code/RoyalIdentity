namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>Protects and restores opaque signing-key material without knowing how it was generated.</summary>
public interface IKeyMaterialProtector
{
	/// <summary>Stable identifier persisted beside material protected by this implementation.</summary>
	string ProtectorId { get; }

	ValueTask<KeyMaterialEnvelope> ProtectAsync(string material, CancellationToken ct = default);

	ValueTask<string> UnprotectAsync(KeyMaterialEnvelope envelope, CancellationToken ct = default);
}
