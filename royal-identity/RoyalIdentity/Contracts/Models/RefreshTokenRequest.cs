using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Models;

public class RefreshTokenRequest
{
    /// <summary>
    /// The HttpContext for the current request.
    /// This is used to get the issuer name for the access token.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    public required ClaimsPrincipal Subject { get; init; }

    public required Client Client { get; init; }

    public required AccessToken AccessToken { get; init; }

    /// <summary>
    /// Claims of the identity token emitted alongside the access token. They are used only when the realm
    /// captures a snapshot and remain separate from <see cref="AccessToken"/> claims so access-only client
    /// claims can never be copied into a renewed identity token.
    /// </summary>
    public IReadOnlyCollection<Claim>? IdentityTokenClaims { get; init; }
}
