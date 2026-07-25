using Microsoft.Extensions.Options;
using RoyalIdentity.Storage.EntityFramework.Security.Cryptography;

namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>
/// Protects signing-key material with authenticated AES-GCM and a fresh nonce per write, over the shared
/// <see cref="AesGcmCipher"/> primitive. Signing-key material carries no authenticated context of its own, so
/// no associated data is supplied here; the operational payload profiles bind theirs.
/// </summary>
public sealed class AesKeyMaterialProtector : IKeyMaterialProtector, IDisposable
{
	public const string Id = "aes-gcm";

	private readonly AesGcmCipher cipher;

	public AesKeyMaterialProtector(IOptions<AesKeyMaterialProtectorOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var configuredKey = options.Value.Key;
		if (configuredKey.Length is not (16 or 24 or 32))
			throw new InvalidOperationException("The AES-GCM signing-key protector requires a 16, 24 or 32 byte key.");

		cipher = new AesGcmCipher(configuredKey);
	}

	public string ProtectorId => Id;

	public ValueTask<KeyMaterialEnvelope> ProtectAsync(string material, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(material);
		ct.ThrowIfCancellationRequested();

		return ValueTask.FromResult(new KeyMaterialEnvelope(ProtectorId, cipher.Encrypt(material, default)));
	}

	public ValueTask<string> UnprotectAsync(KeyMaterialEnvelope envelope, CancellationToken ct = default)
	{
		ValidateEnvelope(envelope);
		ct.ThrowIfCancellationRequested();

		// Cryptographic failures propagate as they are: a tampered ciphertext must stay observable as an
		// AuthenticationTagMismatchException, not be flattened into a generic CryptographicException.
		return ValueTask.FromResult(cipher.Decrypt(envelope.Payload, default));
	}

	public void Dispose() => cipher.Dispose();

	private static void ValidateEnvelope(KeyMaterialEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		if (envelope.Version != KeyMaterialEnvelope.CurrentVersion
			|| !string.Equals(envelope.ProtectorId, Id, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The signing-key material envelope is incompatible with the AES-GCM protector.");
		}
	}
}
