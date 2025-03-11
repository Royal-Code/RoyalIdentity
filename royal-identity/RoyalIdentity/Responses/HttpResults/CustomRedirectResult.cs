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
