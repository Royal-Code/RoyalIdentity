namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Authenticates a user by local credentials: resolves the login identifier (username/email/fictício),
/// verifies the credential and applies lockout, returning a single <see cref="AuthenticationResult"/>.
/// It does NOT start a session, write cookies or decide prompt/consent — that is the IdP's job
/// (fronteira em ADR-014 §2.x). Realm is bound at construction.
/// </summary>
public interface ILocalUserAuthenticator
{
    /// <summary>Authenticates a login + password attempt.</summary>
    Task<AuthenticationResult> AuthenticateLocalAsync(string login, string password, CancellationToken ct = default);
}
