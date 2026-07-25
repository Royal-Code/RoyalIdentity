using System.Security.Cryptography;
using System.Text;

namespace RoyalIdentity.Storage.EntityFramework.Security.Cryptography;

/// <summary>
/// Authenticated AES-GCM over a UTF-8 string, with a fresh nonce per write and optional associated data. It
/// is the generic primitive shared by the signing-key material protector (Configuration) and the operational
/// payload protection profiles: the two keep their own contracts, purposes and authenticated context, and
/// only the cipher mechanics are common (plan-data-operational-storage Fase 1).
/// </summary>
public sealed class AesGcmCipher : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] key;

    /// <summary>Creates a cipher over a copy of a 16, 24 or 32 byte key.</summary>
    public AesGcmCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length is not (16 or 24 or 32))
            throw new ArgumentException("AES-GCM requires a 16, 24 or 32 byte key.", nameof(key));

        this.key = key.ToArray();
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns <c>base64(nonce | tag | ciphertext)</c>.
    /// <paramref name="associatedData"/> is authenticated but not encrypted, so decryption fails when the
    /// context it came from differs.
    /// </summary>
    public string Encrypt(string plaintext, ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plainBytes, ciphertext, tag, associatedData);

            var payload = new byte[NonceSize + TagSize + ciphertext.Length];
            nonce.CopyTo(payload, 0);
            tag.CopyTo(payload, NonceSize);
            ciphertext.CopyTo(payload, NonceSize + TagSize);

            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    /// <summary>
    /// Decrypts a value produced by <see cref="Encrypt"/>. Throws <see cref="CryptographicException"/> when the
    /// payload is malformed, tampered with, or was produced under different associated data.
    /// </summary>
    public string Decrypt(string protectedPayload, ReadOnlySpan<byte> associatedData)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedPayload);

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(protectedPayload);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("The AES-GCM payload is invalid.", exception);
        }

        if (payload.Length <= NonceSize + TagSize)
            throw new CryptographicException("The AES-GCM payload is invalid.");

        var plainBytes = new byte[payload.Length - NonceSize - TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                payload.AsSpan(0, NonceSize),
                payload.AsSpan(NonceSize + TagSize),
                payload.AsSpan(NonceSize, TagSize),
                plainBytes,
                associatedData);

            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}
