using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Models;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Extensions;

internal static class ResponseHandlerExtensions
{
    public static void Error(this IContextBase context, string error, string errorDescription, string? details = null)
    {
        if (details.IsPresent())
            errorDescription = $"{errorDescription}: {details}";

        context.Response = ResponseHandler.Error(error, errorDescription);
    }

    public static void Error(this IContextBase context, ErrorDetails errorDetails)
    {
        context.Response = ResponseHandler.Error(
            errorDetails.Error ?? AuthorizeErrors.InvalidRequest,
            errorDetails.ErrorDescription);
    }

    public static void InvalidRequest(this IContextBase context, string errorDescription)
    {
        context.Response = ResponseHandler.Error(AuthorizeErrors.InvalidRequest, errorDescription);
    }

    public static void InvalidRequest(this IContextBase context, string errorDescription, string? details)
    {
        context.InvalidRequest($"{errorDescription}: {details}");
    }

    public static void InvalidGrant(this IContextBase context, string errorDescription)
    {
        context.Response = ResponseHandler.Error(TokenErrors.InvalidGrant, errorDescription);
    }

    public static void InvalidClient(this IContextBase context, string errorDescription)
    {
        context.Response = ResponseHandler.Error(TokenErrors.InvalidClient, errorDescription);
    }
}
