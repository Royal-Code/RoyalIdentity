using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>Protects signing-key material with authenticated AES-GCM and a fresh nonce per write.</summary>
public sealed class AesKeyMaterialProtector : IKeyMaterialProtector, IDisposable
{
	public const string Id = "aes-gcm";
	private const int NonceSize = 12;
	private const int TagSize = 16;

	private readonly byte[] key;

	public AesKeyMaterialProtector(IOptions<AesKeyMaterialProtectorOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		var configuredKey = options.Value.Key;
		if (configuredKey.Length is not (16 or 24 or 32))
			throw new InvalidOperationException("The AES-GCM signing-key protector requires a 16, 24 or 32 byte key.");

		key = configuredKey.ToArray();
	}

	public string ProtectorId => Id;

	public ValueTask<KeyMaterialEnvelope> ProtectAsync(string material, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(material);
		ct.ThrowIfCancellationRequested();

		var plaintext = Encoding.UTF8.GetBytes(material);
		var nonce = RandomNumberGenerator.GetBytes(NonceSize);
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[TagSize];

		try
		{
			using var aes = new AesGcm(key, TagSize);
			aes.Encrypt(nonce, plaintext, ciphertext, tag);

			var payload = new byte[NonceSize + TagSize + ciphertext.Length];
			nonce.CopyTo(payload, 0);
			tag.CopyTo(payload, NonceSize);
			ciphertext.CopyTo(payload, NonceSize + TagSize);
			return ValueTask.FromResult(new KeyMaterialEnvelope(ProtectorId, Convert.ToBase64String(payload)));
		}
		finally
		{
			CryptographicOperations.ZeroMemory(plaintext);
			CryptographicOperations.ZeroMemory(ciphertext);
		}
	}

	public ValueTask<string> UnprotectAsync(KeyMaterialEnvelope envelope, CancellationToken ct = default)
	{
		ValidateEnvelope(envelope);
		ct.ThrowIfCancellationRequested();

		byte[] payload;
		try
		{
			payload = Convert.FromBase64String(envelope.Payload);
		}
		catch (FormatException exception)
		{
			throw new CryptographicException("The AES-GCM signing-key material payload is invalid.", exception);
		}

		if (payload.Length <= NonceSize + TagSize)
			throw new CryptographicException("The AES-GCM signing-key material payload is invalid.");

		var plaintext = new byte[payload.Length - NonceSize - TagSize];
		try
		{
			using var aes = new AesGcm(key, TagSize);
			aes.Decrypt(
				payload.AsSpan(0, NonceSize),
				payload.AsSpan(NonceSize + TagSize),
				payload.AsSpan(NonceSize, TagSize),
				plaintext);
			return ValueTask.FromResult(Encoding.UTF8.GetString(plaintext));
		}
		finally
		{
			CryptographicOperations.ZeroMemory(plaintext);
			CryptographicOperations.ZeroMemory(payload);
		}
	}

	public void Dispose() => CryptographicOperations.ZeroMemory(key);

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
