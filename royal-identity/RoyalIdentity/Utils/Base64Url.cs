using SecurityBase64Url = RoyalIdentity.Security.Encoding.Base64Url;

namespace RoyalIdentity.Utils;

/// <summary>
/// Delegate wrapper kept for backward compatibility until Phase 7.
/// All members delegate to <see cref="SecurityBase64Url"/>.
/// </summary>
public static class Base64Url
{
    public static string Encode(byte[] arg) => SecurityBase64Url.Encode(arg);

    public static byte[] Decode(string arg) => SecurityBase64Url.Decode(arg);
}
