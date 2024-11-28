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
    ///     Validate the user credentials (password) and return the user if valid.
    /// </para>
    /// <para>
    ///     When the credentials are valid, the method should return
    ///     a <see cref="CredentialsValidationResult"/> with the user.
    /// </para>
    /// <para>
    ///     When the credentials are invalid, the method should return
    ///     a <see cref="CredentialsValidationResult"/> with the reason and the error message.
    /// </para>
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<CredentialsValidationResult> ValidateCredentialsAsync(string username, string password, CancellationToken ct);

    Task SignInAsync(IdentityUser user, IdentitySession? session, bool inputRememberLogin, string amr, CancellationToken ct);
}