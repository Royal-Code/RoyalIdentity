using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts;

public interface ITokenFactory
{
    /// <summary>
    /// Creates an identity token.
    /// </summary>
    /// <param name="request">The id token creation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An identity token</returns>
    Task<Token> CreateIdentityTokenAsync(IdentityTokenRequest request, CancellationToken ct);

    /// <summary>
    /// Creates an access token.
    /// </summary>
    /// <param name="request">The access token creation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An access token</returns>
    Task<AccessToken> CreateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct);
}
