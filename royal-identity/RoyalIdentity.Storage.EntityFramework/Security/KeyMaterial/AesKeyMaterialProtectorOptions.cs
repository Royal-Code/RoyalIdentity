namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>
/// AES-GCM key supplied by the consumer. RoyalIdentity deliberately does not prescribe how this value is
/// loaded; production composition may obtain it from KMS, a key vault or another secure source.
/// </summary>
public sealed class AesKeyMaterialProtectorOptions
{
	/// <summary>An AES key containing exactly 16, 24 or 32 bytes.</summary>
	public byte[] Key { get; set; } = [];
}
