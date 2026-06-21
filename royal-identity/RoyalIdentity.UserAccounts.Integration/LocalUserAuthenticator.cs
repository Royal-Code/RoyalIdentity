using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Module-backed <see cref="ILocalUserAuthenticator"/> (ADR-015 §2.1): delegates to the module's
/// <see cref="AuthenticateLocalCredential"/> use case (which resolves the login, verifies the credential and
/// persists the lockout/failed-attempt state inside its unit of work) and maps the internal outcome onto the
/// edge <see cref="AuthenticationResult"/>. The realm and its policies are bound at construction.
/// <para>
/// The internal failure reason is preserved (mapped, not collapsed): the IdP renders a single generic message
/// for anti-enumeration, but keeps the reason for events/audit. <see cref="LocalAuthenticationFailureReason.LockedOut"/>
/// maps to <see cref="AuthenticationFailureReason.Blocked"/> per the plan (administrative block OR active lockout
/// surface as <c>Blocked</c>); <see cref="LocalAuthenticationFailureReason.PasswordNotSet"/> maps to
/// <see cref="AuthenticationFailureReason.InvalidCredentials"/> (no password ⇒ password auth unavailable).
/// </para>
/// </summary>
public sealed class LocalUserAuthenticator(
    IAuthenticateLocalCredentialHandler authenticate,
    string realmId,
    UserAccountsRealmOptions options) : ILocalUserAuthenticator
{
    /// <inheritdoc />
    public async Task<AuthenticationResult> AuthenticateLocalAsync(
        string login, string password, CancellationToken ct = default)
    {
        var command = new AuthenticateLocalCredential
        {
            RealmId = realmId,
            Options = options,
            Login = login,
            Password = password
        };

        var result = await authenticate.HandleAsync(command, ct);

        // A problem result means the attempt could not even be evaluated (e.g. empty login/password). Surface it
        // as the generic invalid-credentials failure — never leak validation detail to the edge (anti-enumeration).
        if (!result.HasValue(out var outcome))
        {
            return AuthenticationResult.Failed(AuthenticationFailureReason.InvalidCredentials);
        }

        if (outcome.Success)
        {
            // Success only ever occurs for an active account, so IsActive is true here.
            var subject = new Subject(outcome.SubjectId!, outcome.DisplayName!, IsActive: true);
            return AuthenticationResult.Succeeded(subject);
        }

        return AuthenticationResult.Failed(MapReason(outcome.Reason!.Value));
    }

    private static AuthenticationFailureReason MapReason(LocalAuthenticationFailureReason reason) => reason switch
    {
        LocalAuthenticationFailureReason.NotFound => AuthenticationFailureReason.NotFound,
        LocalAuthenticationFailureReason.Inactive => AuthenticationFailureReason.Inactive,
        LocalAuthenticationFailureReason.Blocked => AuthenticationFailureReason.Blocked,
        LocalAuthenticationFailureReason.LockedOut => AuthenticationFailureReason.Blocked,
        // PasswordNotSet and InvalidCredentials both surface as the generic invalid-credentials reason.
        _ => AuthenticationFailureReason.InvalidCredentials,
    };
}
