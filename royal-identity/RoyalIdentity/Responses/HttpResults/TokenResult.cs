// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class TokenResult : IResult
{
    private readonly TokenEndpointParameters values;

    public TokenResult(TokenEndpointParameters values)
    {
        this.values = values;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetNoCache();
        return httpContext.Response.WriteJsonAsync(values);
    }
}
