using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class ConsentPageResult(IEndpointContextBase context) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => StatusCodes.Status302Found;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var returnUrl = Oidc.Routes.BuildAuthorizeCallbackUrl(context.Realm.Path).EnsureLeadingSlash();

        if (context.Realm.Options.StoreAuthorizationParameters)
        {
            var storage = httpContext.RequestServices.GetRequiredService<IStorage>();
            var id = await storage.AuthorizeParameters.WriteAsync(context.Raw, httpContext.RequestAborted);
            returnUrl = returnUrl.AddQueryString(Oidc.Routes.Params.Authorization, id);
        }
        else
        {
            returnUrl = returnUrl.AddQueryString(context.Raw.ToQueryString());
        }

        var consentUrl = context.Realm.Routes.ConsentPath;

        if (!consentUrl.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're 
            // redirecting to a different server
            returnUrl = httpContext.GetRealmPath().EnsureTrailingSlash() + returnUrl.RemoveLeadingSlash();
        }

        var url = consentUrl.AddQueryString(context.Realm.Options.UI.ConsentParameter, returnUrl);
        httpContext.Response.RedirectToAbsoluteUrl(url);
    }
}
