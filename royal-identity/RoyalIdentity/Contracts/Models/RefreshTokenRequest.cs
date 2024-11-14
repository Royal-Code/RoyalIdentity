using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Models;

public class RefreshTokenRequest
{
    public required IWithClient Context { get; init; }

    public required NameValueCollection Raw { get; init; }

    public required ClaimsPrincipal Subject { get; init; }

    public required AccessToken AccessToken { get; init; }

    public required string Caller { get; init; }
}