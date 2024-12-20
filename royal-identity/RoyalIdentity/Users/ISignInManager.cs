using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Users;

/// <summary>
/// Provides the APIs for user sign in.
/// </summary>
public interface ISignInManager
{
    /// <summary>
    /// Gets the authorization context.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<AuthorizationContext?> GetAuthorizationContextAsync(string? returnUrl, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Validate the user credentials (password), start a session for the user,
    ///     returning the user and the session if the credentials are valid.
    /// </para>
    /// <para>
    ///     When the credentials are valid, the method should return
    ///     a <see cref="CredentialsValidationResult"/> with the user and the session.
    /// </para>
    /// <para>
    ///     When the credentials are invalid, the method should return
    ///     a <see cref="CredentialsValidationResult"/> with the reason and the error message.
    /// </para>
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The credentials validation result.</returns>
    Task<CredentialsValidationResult> AuthenticateUserAsync(string username, string password, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Once the user is authenticated, sign in the user.
    /// </para>
    /// <para>
    ///     This method should authenticate the user on the AspNetCore host/service, 
    ///     using the authentication method (scheme) defined in the program configuration.
    /// </para>
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="session">The session.</param>
    /// <param name="inputRememberLogin">Indicates if the user wants to be remembered.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task SignInAsync(IdentityUser user, IdentitySession? session, bool inputRememberLogin, CancellationToken ct);
}