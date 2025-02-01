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
}