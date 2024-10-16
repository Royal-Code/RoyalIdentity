using System.Security.Claims;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
/// The claims service is responsible for determining which claims to include in tokens
/// </summary>
public interface ITokenClaimsService
{
    /// <summary>
    /// Returns claims for an identity token
    /// </summary>
    /// <param name="subject">The subject</param>
    /// <param name="resources">The resources.</param>
    /// <param name="includeAllIdentityClaims">Specifies if all claims should be included in the token, or if the userinfo endpoint can be used to retrieve them</param>
    /// <param name="context">The raw context</param>
    /// <returns>
    /// Claims for the identity token
    /// </returns>
    Task<IEnumerable<Claim>> GetIdentityTokenClaimsAsync(ClaimsPrincipal subject, Resources resources, bool includeAllIdentityClaims, IWithClient context);

    /// <summary>
    /// Returns claims for an access token.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="resources">The resources.</param>
    /// <param name="context">The raw context.</param>
    /// <returns>
    /// Claims for the access token
    /// </returns>
    Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(ClaimsPrincipal subject, Resources resources, IWithClient context);
}