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
}
