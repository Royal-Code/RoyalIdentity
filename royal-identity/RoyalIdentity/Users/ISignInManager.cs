using RoyalIdentity.Models;
using RoyalIdentity.Users.Contexts;
using System.Security.Claims;

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
    /// <param name="realm">The realm.</param>
    /// <param name="username">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The credentials validation result.</returns>
    Task<CredentialsValidationResult> AuthenticateUserAsync(Realm realm, string username, string password, CancellationToken ct);

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
    /// <param name="remember">Indicates if the user wants to be remembered.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task<ClaimsPrincipal> SignInAsync(IdentityUser user, IdentitySession? session, bool remember, CancellationToken ct);

    /// <summary>
    /// <para>
    ///     Verify if the user consent is required.
    /// </para>
    /// </summary>
    /// <param name="user">The user.</param> 
    /// <param name="client">The client.</param>
    /// <param name="resources">The requested resources.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user consent is required, otherwise false.</returns>
    Task<bool> ConsentRequired(ClaimsPrincipal user, Client client, Resources resources, CancellationToken ct);
}