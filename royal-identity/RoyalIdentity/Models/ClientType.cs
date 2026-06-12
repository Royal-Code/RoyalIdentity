namespace RoyalIdentity.Models;

/// <summary>
/// The OAuth 2.0 client type (RFC 6749, section 2.1).
/// </summary>
public enum ClientType
{
    /// <summary>
    /// A client incapable of keeping its credentials confidential
    /// (e.g. single-page apps, native/mobile apps).
    /// </summary>
    Public = 0,

    /// <summary>
    /// A client capable of keeping its credentials confidential
    /// (e.g. server-side web apps, backend services).
    /// </summary>
    Confidential = 1,
}
