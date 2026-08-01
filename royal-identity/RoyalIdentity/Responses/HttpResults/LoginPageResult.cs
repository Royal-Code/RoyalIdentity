// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class LoginPageResult(IEndpointContextBase context) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => StatusCodes.Status302Found;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var returnUrl = Oidc.Routes.BuildAuthorizeCallbackUrl(context.Realm.Path).EnsureLeadingSlash();

        if (context.Realm.Options.StoreAuthorizationParameters)
        {
            var storage = httpContext.RequestServices.GetRequiredService<IStorage>();
            var id = await storage.GetAuthorizeParametersStore(context.Realm)
                .WriteAsync(context.Raw, httpContext.RequestAborted);
            returnUrl = returnUrl.AddQueryString(Oidc.Routes.Params.Authorization, id);
        }
        else
        {
            returnUrl = returnUrl.AddQueryString(context.Raw.ToQueryString());
        }

        var loginUrl = context.Realm.Routes.LoginPath;

        if (!loginUrl.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're 
            // redirecting to a different server
            returnUrl = httpContext.GetRealmPath().EnsureTrailingSlash() + returnUrl.RemoveLeadingSlash();
        }

        var url = loginUrl.AddQueryString(context.Realm.Options.UI.LoginParameter, returnUrl);
        httpContext.Response.RedirectToAbsoluteUrl(url);
    }
}
