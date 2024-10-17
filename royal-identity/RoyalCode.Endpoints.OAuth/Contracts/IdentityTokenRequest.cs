using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Security.Claims;

namespace RoyalIdentity.Contracts;

public class IdentityTokenRequest
{
    public required IWithClient Context { get; init; }

    public required NameValueCollection Raw { get; init; }

    public required ClaimsPrincipal Subject { get; init; }

    public required Resources Resources { get; init; }

    public required string Caller { get; init; }

    public string? Nonce { get; init; }

    public string? AccessTokenToHash { get; init; }

    public string? AuthorizationCodeToHash { get; init; }

    public string? StateHash { get; init; }
}
