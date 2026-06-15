using System.Collections.Concurrent;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// In-memory (fake/reference) <see cref="ILocalUserAuthenticator"/>: resolves the login identifier,
/// verifies the password (<see cref="IPasswordProtector"/>) and applies the single <see cref="LockoutPolicy"/>,
/// returning one <see cref="AuthenticationResult"/>. It does NOT start a session or write cookies — that is
/// the IdP's job (ADR-014 §2.x). Realm is bound at construction. Failure/success counters are mutated
/// in place on the shared <see cref="MemoryUserAccount"/> records (the fake store's "persistence").
/// </summary>
public sealed class MemoryLocalUserAuthenticator(
    ConcurrentDictionary<string, MemoryUserAccount> users,
    AccountOptions accountOptions,
    IPasswordProtector passwordProtector,
    LockoutPolicy lockoutPolicy) : ILocalUserAuthenticator
{
    public async Task<AuthenticationResult> AuthenticateLocalAsync(
        string login, string password, CancellationToken ct = default)
    {
        var details = ResolveLogin(login);
        if (details is null)
            return AuthenticationResult.Failed(AuthenticationFailureReason.NotFound);

        if (!details.IsActive)
            return AuthenticationResult.Failed(AuthenticationFailureReason.Inactive);

        if (lockoutPolicy.IsLockedOut(details))
            return AuthenticationResult.Failed(AuthenticationFailureReason.Blocked);

        // No password hash ⇒ password authentication is not available for this account.
        if (details.PasswordHash is null)
            return AuthenticationResult.Failed(AuthenticationFailureReason.InvalidCredentials);

        var valid = await passwordProtector.VerifyPasswordAsync(password, details.PasswordHash, ct);
        if (!valid)
        {
            lockoutPolicy.RegisterFailure(details);
            return AuthenticationResult.Failed(AuthenticationFailureReason.InvalidCredentials);
        }

        lockoutPolicy.RegisterSuccess(details);
        return AuthenticationResult.Succeeded(new Subject(details.SubjectId, details.DisplayName, details.IsActive));
    }

    /// <summary>
    /// Resolves the login identifier to an account: by username (exact, then case-insensitive) and, when the
    /// realm allows it, by email claim. Identifier resolution is the authenticator's job, not the store's
    /// (pontos1 §2).
    /// </summary>
    private MemoryUserAccount? ResolveLogin(string login)
    {
        if (users.TryGetValue(login, out var byKey))
            return byKey;

        var byUsername = users.Values.FirstOrDefault(
            u => string.Equals(u.Username, login, StringComparison.OrdinalIgnoreCase));
        if (byUsername is not null)
            return byUsername;

        if (accountOptions.LoginWithEmail || accountOptions.EmailAsUsername)
        {
            return users.Values.FirstOrDefault(
                u => u.Claims.Any(c => c.Type == "email" && string.Equals(c.Value, login, StringComparison.OrdinalIgnoreCase)));
        }

        return null;
    }
}
