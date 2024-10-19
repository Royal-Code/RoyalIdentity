using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Responses.HttpResults;

public class ConsentPageResult(IEndpointContextBase context) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => StatusCodes.Status302Found;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var authorizeParametersStore = httpContext.RequestServices.GetService<IAuthorizeParametersStore>();
        var options = httpContext.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value;

        var returnUrl = httpContext.GetServerBasePath().EnsureTrailingSlash() + Constants.ProtocolRoutePaths.AuthorizeCallback;

        if (authorizeParametersStore != null)
        {
            var id = await authorizeParametersStore.WriteAsync(context.Raw, httpContext.RequestAborted);
            returnUrl = returnUrl.AddQueryString(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, id);
        }
        else
        {
            returnUrl = returnUrl.AddQueryString(context.Raw.ToQueryString());
        }

        var consentUrl = options.UserInteraction.ConsentUrl;
        if (!consentUrl.IsLocalUrl())
        {
            // this converts the relative redirect path to an absolute one if we're 
            // redirecting to a different server
            returnUrl = httpContext.GetServerHost().EnsureTrailingSlash() + returnUrl.RemoveLeadingSlash();
        }

        var url = consentUrl.AddQueryString(options.UserInteraction.ConsentReturnUrlParameter, returnUrl);
        httpContext.Response.RedirectToAbsoluteUrl(url);
    }
}
