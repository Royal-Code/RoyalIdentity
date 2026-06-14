using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Resolves the <see cref="AuthorizationContext"/> from a returnUrl for the login/consent/logout screens
/// (Q2, ADR-014 §2.4). Replaces <c>ISignInManager.GetAuthorizationContextAsync</c>;
/// <c>SessionContextService</c> (Razor) delegates here.
/// </summary>
public interface IAuthorizationContextResolver
{
    /// <summary>Resolves the authorization context for the returnUrl, or <c>null</c> when absent/invalid.</summary>
    Task<AuthorizationContext?> ResolveAsync(string? returnUrl, CancellationToken ct = default);
}
