using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Responses.HttpResults;

public class LoginPageResult(IEndpointContextBase context) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => StatusCodes.Status302Found;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var returnUrl = $"/{context.Realm.Path}/{Constants.ProtocolRoutePaths.AuthorizeCallback}";

        if (context.Realm.Options.StoreAuthorizationParameters)
        {
            var storage = httpContext.RequestServices.GetRequiredService<IStorage>();
            var id = await storage.AuthorizeParameters.WriteAsync(context.Raw, httpContext.RequestAborted);
            returnUrl = returnUrl.AddQueryString(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, id);
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
