using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Models;

public class IdentityTokenRequest
{
    /// <summary>
    /// The HttpContext for the current request.
    /// This is used to get the issuer name for the access token.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    public required ClaimsPrincipal User { get; init; }

    public required Client Client { get; init; }

    public required Resources Resources { get; init; }

    public required string Caller { get; init; }

    public string? Nonce { get; init; }

    public string? AccessTokenToHash { get; init; }

    public string? AuthorizationCodeToHash { get; init; }

    public string? StateHash { get; init; }
}
