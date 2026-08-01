using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using RoyalIdentity.Security.Encoding;

namespace RoyalIdentity.Authentication;

/// <summary>
/// The versioned, origin-bound representation used by OpenID Connect Session Management.
/// </summary>
internal static class SessionStateFormat
{
    internal const string Version = "v1";
    internal const int HashSize = 32;
    internal const int SaltSize = 32;

    private static readonly UTF8Encoding strictUtf8 = new(false, true);

    internal static string Create(string clientId, string origin, string userAgentState)
        => Create(
            clientId,
            origin,
            userAgentState,
            RoyalIdentity.Security.Cryptography.CryptoRandom.CreateRandomKey(SaltSize));

    internal static string Create(
        string clientId,
        string origin,
        string userAgentState,
        ReadOnlySpan<byte> salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        if (!CheckSessionStateManager.IsValidState(userAgentState))
        {
            throw new ArgumentException(
                "The OP User Agent State must be a canonical 256-bit Base64Url value.",
                nameof(userAgentState));
        }

        var canonicalOrigin = GetOrigin(origin);
        if (!string.Equals(origin, canonicalOrigin, StringComparison.Ordinal))
            throw new ArgumentException("The origin must be in its canonical form.", nameof(origin));

        if (salt.Length != SaltSize)
            throw new ArgumentException($"The salt must contain exactly {SaltSize} bytes.", nameof(salt));

        var canonical = CreateCanonicalBytes(clientId, origin, userAgentState, salt);
        var hash = SHA256.HashData(canonical);
        var originBytes = strictUtf8.GetBytes(origin);

        return string.Join(
            '.',
            Version,
            Base64Url.Encode(originBytes),
            Base64Url.Encode(hash),
            Base64Url.Encode(salt));
    }

    internal static bool TryParse(string? value, out ParsedSessionState? parsed)
    {
        parsed = null;

        if (string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace))
            return false;

        var segments = value.Split('.');
        if (segments.Length != 4 || !string.Equals(segments[0], Version, StringComparison.Ordinal))
            return false;

        if (!TryDecodeCanonical(segments[1], out var originBytes)
            || !TryDecodeCanonical(segments[2], out var hash)
            || !TryDecodeCanonical(segments[3], out var salt)
            || hash.Length != HashSize
            || salt.Length != SaltSize)
        {
            return false;
        }

        string origin;
        try
        {
            origin = strictUtf8.GetString(originBytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        string canonicalOrigin;
        try
        {
            canonicalOrigin = GetOrigin(origin);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!string.Equals(origin, canonicalOrigin, StringComparison.Ordinal))
            return false;

        parsed = new ParsedSessionState(origin, hash, salt);
        return true;
    }

    internal static string GetOrigin(string uriValue)
    {
        if (!TryGetOrigin(uriValue, out var origin))
        {
            throw new ArgumentException(
                "A session state origin must be an absolute HTTP or HTTPS URI.",
                nameof(uriValue));
        }

        return origin;
    }

    internal static bool TryGetOrigin(string? uriValue, [NotNullWhen(true)] out string? origin)
    {
        origin = null;

        if (!Uri.TryCreate(uriValue, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{IPAddress.Parse(uri.IdnHost)}]"
            : uri.IdnHost;

        origin = $"{uri.Scheme}://{host}";
        if (!uri.IsDefaultPort)
            origin += $":{uri.Port}";

        return true;
    }

    internal static byte[] CreateCanonicalBytes(
        string clientId,
        string origin,
        string userAgentState,
        ReadOnlySpan<byte> salt)
    {
        using var stream = new MemoryStream();
        AppendField(stream, strictUtf8.GetBytes(Version));
        AppendField(stream, strictUtf8.GetBytes(clientId));
        AppendField(stream, strictUtf8.GetBytes(origin));
        AppendField(stream, strictUtf8.GetBytes(userAgentState));
        AppendField(stream, salt);
        return stream.ToArray();
    }

    private static void AppendField(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    private static bool TryDecodeCanonical(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(value) || !Base64Url.TryDecode(value, out bytes))
            return false;

        return string.Equals(value, Base64Url.Encode(bytes), StringComparison.Ordinal);
    }
}

internal sealed record ParsedSessionState(string Origin, byte[] Hash, byte[] Salt);
