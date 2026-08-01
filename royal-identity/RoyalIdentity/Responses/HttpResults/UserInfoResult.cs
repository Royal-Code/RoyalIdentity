// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class UserInfoResult : IResult, IStatusCodeHttpResult
{
    private readonly IDictionary<string, object> userData;

    public UserInfoResult(IDictionary<string, object> userData)
    {
        this.userData = userData;
    }

    public int? StatusCode => StatusCodes.Status200OK;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetNoCache();
        await httpContext.Response.WriteJsonAsync(userData);
    }
}
