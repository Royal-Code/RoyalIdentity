namespace RoyalIdentity.Utils;

/// <summary>
/// Describes the string so we know what to search for in certificate store
/// </summary>
public enum NameType
{
    /// <summary>
    /// subject distinguished name
    /// </summary>
    SubjectDistinguishedName,

    /// <summary>
    /// thumbprint
    /// </summary>
    Thumbprint
}