using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Responses.HttpResults;

public class CustomRedirectResult(IEndpointContextBase context, string redirectUrl) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => StatusCodes.Status302Found;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var options = httpContext.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value;

        var returnUrl = httpContext.GetServerBasePath().EnsureTrailingSlash() + Constants.ProtocolRoutePaths.Authorize;
        returnUrl = returnUrl.AddQueryString(context.Raw.ToQueryString());

        if (!redirectUrl.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're 
            // redirecting to a different server
            returnUrl = httpContext.GetServerBaseUrl().EnsureTrailingSlash() + returnUrl.RemoveLeadingSlash();
        }

        var url = redirectUrl.AddQueryString(options.UI.CustomRedirectParameter, returnUrl);
        httpContext.Response.RedirectToAbsoluteUrl(url);

        return Task.CompletedTask;
    }
}
