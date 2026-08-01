// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class CustomRedirectResult(IEndpointContextBase context, string redirectUrl) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => StatusCodes.Status302Found;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var options = context.Realm.Options.ServerOptions.UI;

        var returnUrl = httpContext.GetServerBasePath().EnsureTrailingSlash() + Oidc.Routes.BuildAuthorizeUrl(context.Realm.Path);
        returnUrl = returnUrl.AddQueryString(context.Raw.ToQueryString());

        if (!redirectUrl.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're 
            // redirecting to a different server
            returnUrl = httpContext.GetServerBaseUrl().EnsureTrailingSlash() + returnUrl.RemoveLeadingSlash();
        }

        var url = redirectUrl.AddQueryString(options.CustomRedirectParameter, returnUrl);
        httpContext.Response.RedirectToAbsoluteUrl(url);

        return Task.CompletedTask;
    }
}
