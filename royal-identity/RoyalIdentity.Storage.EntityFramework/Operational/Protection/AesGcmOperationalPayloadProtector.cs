using System.Security.Cryptography;
using RoyalIdentity.Storage.EntityFramework.Security.Cryptography;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Operational payload profile backed by authenticated AES-GCM, binding the
/// <see cref="OperationalProtectionContext"/> as associated data. The key is supplied by the composition
/// (KMS, vault, or another secure source); RoyalIdentity never prescribes how it is obtained and it never
/// enters the Configuration payload (plan DF30).
/// </summary>
public sealed class AesGcmOperationalPayloadProtector : IOperationalPayloadProtector, IDisposable
{
	private readonly AesGcmCipher cipher;

	public AesGcmOperationalPayloadProtector(string profileId, ReadOnlySpan<byte> key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

		ProfileId = profileId;
		cipher = new AesGcmCipher(key);
	}

	public string ProfileId { get; }

	public ValueTask<string> ProtectAsync(
		string payload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(payload);
		ArgumentNullException.ThrowIfNull(context);
		ct.ThrowIfCancellationRequested();

		return ValueTask.FromResult(cipher.Encrypt(payload, context.ToAssociatedData()));
	}

	public ValueTask<string> UnprotectAsync(
		string protectedPayload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(protectedPayload);
		ArgumentNullException.ThrowIfNull(context);
		ct.ThrowIfCancellationRequested();

		try
		{
			return ValueTask.FromResult(cipher.Decrypt(protectedPayload, context.ToAssociatedData()));
		}
		catch (CryptographicException exception)
		{
			throw OperationalPayloadProtectionException.Unreadable(ProfileId, exception);
		}
	}

	public void Dispose() => cipher.Dispose();
}
