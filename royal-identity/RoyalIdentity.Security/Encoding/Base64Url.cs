namespace RoyalIdentity.Security.Encoding;

/// <summary>
/// URL-safe Base64 (RFC 4648 §5) without padding. Thin facade over the BCL
/// <see cref="System.Buffers.Text.Base64Url"/> (.NET 9+): it keeps the short name consumed across the
/// codebase and the no-padding semantics of the values generated today, while delegating the actual
/// conversion to the framework instead of re-implementing it by hand.
/// </summary>
/// <remarks>
/// This type lives in namespace <c>RoyalIdentity.Security.Encoding</c>; any use of
/// <see cref="System.Text.Encoding"/> here must be fully qualified to avoid ambiguity with this namespace
/// and with the BCL <c>System.Buffers.Text.Base64Url</c>.
/// </remarks>
public static class Base64Url
{
    /// <summary>Encodes <paramref name="bytes"/> as URL-safe Base64 without padding.</summary>
    public static string Encode(ReadOnlySpan<byte> bytes)
        => System.Buffers.Text.Base64Url.EncodeToString(bytes);

    /// <summary>
    /// Decodes a URL-safe Base64 string (with or without padding).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="value"/> is not valid Base64Url.</exception>
    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return System.Buffers.Text.Base64Url.DecodeFromChars(value);
    }

    /// <summary>
    /// Attempts to decode a URL-safe Base64 string. Returns <see langword="false"/> (and an empty array)
    /// for <see langword="null"/> or malformed input instead of throwing.
    /// </summary>
    public static bool TryDecode(string value, out byte[] bytes)
    {
        // Note: the BCL TryDecodeFromChars still throws FormatException on invalid content (its "Try" only
        // covers the destination buffer size), so validity is checked up front with the non-throwing IsValid.
        if (value is null || !System.Buffers.Text.Base64Url.IsValid(value))
        {
            bytes = [];
            return false;
        }

        bytes = System.Buffers.Text.Base64Url.DecodeFromChars(value);
        return true;
    }
}
