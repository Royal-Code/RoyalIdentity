using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Options;

namespace RoyalIdentity.Extensions;

internal static class ResponseHandlerExtensions
{
    public static void RespondWithProblem(this IContextBase context, ProblemDetails problem)
    {
        context.Response = ResponseHandler.Problem(problem);
    }

    public static void InvalidRequest(this IContextBase context, string errorDescription)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Status = StatusCodes.Status400BadRequest,
            Title = OidcConstants.AuthorizeErrors.InvalidRequest,
            Detail = errorDescription
        };

        context.Response = ResponseHandler.Problem(problemDetails);
    }

    public static void InvalidRequest(this IContextBase context, string errorDescription, string? details)
    {
        context.InvalidRequest($"{errorDescription}: {details}");
    }
}
