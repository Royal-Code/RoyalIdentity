using RoyalIdentity.Security.Cryptography;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// Produces the handle that identifies a stored authorize request. It is a seam mostly so a test can force a
/// collision — the store must regenerate internally rather than overwrite or fail by bad luck (plan DF16).
/// </summary>
public interface IAuthorizeParametersHandleGenerator
{
    /// <summary>A fresh handle with at least 128 bits of entropy.</summary>
    string Generate();
}

/// <summary>
/// The production generator: 16 cryptographically random bytes — 128 bits — so the handle is not guessable.
/// Only its digest is persisted (plan DF38); the value itself lives in the redirect URL.
/// </summary>
internal sealed class CryptoRandomAuthorizeParametersHandleGenerator : IAuthorizeParametersHandleGenerator
{
    public string Generate() => CryptoRandom.CreateUniqueId(16);
}
